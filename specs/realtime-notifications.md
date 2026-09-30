Push notifications to the web app in real time and make every notification read cost the same at ten users or ten thousand

Today the web app learns about notifications by polling `GET /notifications/unread-count` every 30
seconds from every open tab, whether anything changed or not — 10,000 signed-in tabs are ~330
requests a second doing nothing. Each of those requests is also more expensive than it should be:
`NotificationService.GetUnreadCountAsync` loads **every** `NotificationVisit` id the user has ever
created into memory and sends them back to PostgreSQL as a `NOT IN` list, and the list endpoint runs
three correlated visit subqueries per row. Read state for audience rows (tenant-wide and
platform-wide) is one `NotificationVisit` row per user per notification, so "mark all as read" in a
tenant of 5,000 members writes up to 5,000 rows per broadcast, and nothing is ever deleted.

After this change the server **pushes** each new notification to exactly the connections that may see
it, over SignalR scaled out through the Redis the API already requires; the web app stops polling
while it is connected; and the read paths are bounded by indexes and a per-user read cursor rather
than by how many notifications and visits have piled up.

## Scope

### Read state that does not grow with the audience

- A new entity `NotificationReadCursor` (`UserId`, `TenantId?`, `ReadAllAt`) in the notifications
  schema, unique on (`UserId`, `TenantId`) with nulls **not** distinct (`TenantId == null` is the
  platform-wide audience). An audience notification is read for a user when it has a visit row for
  that user, whatever `CreatedAt` is, or — with no visit row — when its `CreatedAt` is at or before the
  user's cursor for its audience (the tenant's cursor for a tenant-wide row, the null cursor for a
  platform-wide row).
- `NotificationVisit` gains `IsRead` (existing rows migrate as `true`). Mark-as-read on an audience row
  upserts a visit with `IsRead = true`; mark-as-unread upserts one with `IsRead = false`, which is what
  lets a row older than the cursor be marked unread again. Personal rows keep using
  `Notification.IsRead`, unchanged.
- `mark-all-as-read` becomes: the existing bulk `ExecuteUpdate` for the caller's personal rows in the
  active scope, plus an upsert of the caller's cursor to `now()` for the active tenant's audience (when
  acting in one) **and** for the platform-wide audience, plus deleting the caller's visit rows that the
  new cursors now make redundant (those with `IsRead = true` on notifications created at or before the
  cursor). No per-notification insert remains. The semantics users see do not change: platform-wide
  rows marked read in one scope stay read in every scope, exactly as today.
- Every surface that decides read state — `GetUnreadCountAsync`, the list's `isRead` filter, its
  default ordering and its `IsRead` projection, `GET {id}` and the two mark endpoints — goes through one
  internal query helper next to `VisibleTo` in `Core/NotificationQueries.cs`, so the rule is written
  once. The existing `AcrossAllTenants().VisibleTo(...)` rule for *which* rows are visible is not
  changed.
- `GetUnreadCountAsync` runs as a single SQL query (no id list materialized in memory) and stops
  counting at 100: the response's `Count` never exceeds 100, and the web badge shows `99+` above 99.

### Indexes and retention

- Replace the `TitleKey`, `MessageKey` and `CreatedAt` single-column indexes (the list's search is
  `ILIKE '%…%'`, which a b-tree cannot serve) with partial indexes shaped like the reads:
  personal rows `(TenantId, UserId, IsRead, CreatedAt DESC) WHERE UserId IS NOT NULL AND NOT IsDeleted`,
  audience rows `(TenantId, CreatedAt DESC) WHERE UserId IS NULL AND NOT IsDeleted`, and
  `(UserId, NotificationId)` on visits (the existing unique index may serve; keep one, not two).
- A `NotificationOptions` section (`Notifications:RetentionDays`, default 90, `ValidateOnStart`, > 0).
  A Hangfire recurring job, registered daily at the end of `Program.cs` beside the token clean-up jobs,
  hard-deletes notifications — soft-deleted or not — whose `CreatedAt` is older than the retention, in
  batches of at most 5,000 rows per statement so it never holds a long lock; visits cascade. The same
  job deletes visit rows made redundant by a cursor. It establishes no tenant scope and says so: it
  works across all tenants by design, through `AcrossAllTenants()` or hand-written SQL with no tenant
  predicate, and deletes nothing else.

### Real-time delivery over SignalR

- A `NotificationHub` at `/hubs/notifications`, mapped in `Program.cs` behind the same
  `Jwt_Or_Cookie` authentication and the existing `SessionPrincipalValidator`, so a connection is
  refused exactly when an HTTP request with the same credentials would be (401), and answered 503
  `sessionStoreUnavailable` when the store is down. Browsers authenticate with the auth cookie; a bearer
  client passes the access token in the `access_token` query string, which is accepted **only** on the
  hub path, never logged, and makes the policy scheme pick JWT bearer for that request.
- The hub is **server-to-client only**: it declares no client-callable methods. On connect the server
  adds the connection to groups derived from the projected session alone — the client never names a
  group: `u:{userId}:{tenantId|platform}` (personal rows in the acting scope), `t:{tenantId}` (when
  acting in a tenant) and `all` (platform-wide). A connection therefore receives exactly the rows
  `VisibleTo` would list for its session, and nothing from another tenant or scope.
- Transport is WebSockets only, with the client skipping negotiation, so no sticky sessions are needed
  behind a load balancer. Long polling and SSE are disabled on the server.
- Scale-out: outside `Testing`, SignalR uses the Redis backplane on `ConnectionStrings:Redis`, with its
  channel prefix taken from `Redis:InstanceName` so several applications can share one server. Under
  `Testing` it runs in-process with no backplane; the suite keeps needing PostgreSQL only.
- `HubOptions`: keep-alive and client-timeout left at SignalR's defaults unless a reason is written
  beside a change; `MaximumReceiveMessageSize` small (the hub receives nothing); and
  `CloseOnAuthenticationExpiration = true`, so no connection outlives `Auth:AccessTokenValidity`.
- A per-account cap on concurrent hub connections per API instance (`Notifications:MaxConnectionsPerUser`,
  default 20); a connection over the cap is refused and logged.
- **A connection ends with its session.** Every deletion of a session record — revocation through
  `ISessionRevocationService`, sign-out, and the replacement on refresh and tenant switch/exit — closes
  every hub connection authenticated by that `sid`, on every API instance. Identity publishes the ended
  session ids through a narrow `[AllowOutside]` interface (over Redis pub/sub outside `Testing`,
  in-process under it); Notifications subscribes and aborts the matching connections, which it tracks
  per instance by `sid`. Identity takes no dependency on Notifications, and `FeatureDependencyTests`
  stays green.

### Publishing

- `INotificationService` keeps its three methods and their signatures, so every existing caller keeps
  compiling. After a notification is saved, the service publishes a `notificationReceived` event with
  the list DTO's fields (`id`, `type`, `titleKey`, `messageKey`, `group`, `metadata`, `createdAt`,
  `isRead: false`) to the matching group — one message per notification, whatever the audience size.
- A publish happens only once the row is committed. When the caller's `AppDbContext` has an open
  transaction, the publish is deferred until it commits and dropped if it rolls back (a
  `DbTransactionInterceptor` or equivalent). A publish failure is logged and never fails the raise; the
  web app's reconciliation (below) covers a lost message.
- New `NewUserNotificationsAsync(IReadOnlyCollection<Guid> userIds, …)` on `INotificationService` for
  telling many accounts the same thing in the active scope: one batched insert (chunks of at most
  1,000), one publish per recipient group. Document it in the `notifications` skill.
- When the caller's read state changes (mark-as-read, mark-as-unread, mark-all-as-read, delete), the
  endpoint publishes `unreadCountChanged` with the caller's recomputed, capped count to
  `u:{userId}:{scope}` only, so the caller's other tabs and devices update without polling. Nothing is
  recomputed per member for a broadcast.
- Raising from a Hangfire job works unchanged: the job opens the scope as today, and the hub context
  publishes through the backplane from whichever process runs the job.

### Web app

- Add `@microsoft/signalr`. `useNotificationHub` (still mounted once, in `components/layouts/header.tsx`)
  opens one connection when the caller may read notifications (the existing `canReadNotifications`
  rule), and stops it on sign-out and when that rule stops holding.
- On `notificationReceived`: increment `notificationsSlice.unreadCount` (capped display as above) and
  invalidate the `Notifications` list tag so an open panel or list page shows the row — but not the
  `UNREAD_COUNT` tag, so a broadcast to 10,000 users does not make 10,000 count requests. On
  `unreadCountChanged`: set the count.
- Reconnect with `withAutomaticReconnect` using exponential back-off **with random jitter**, capped at
  60 s, so a deploy that drops every connection does not bring them all back in the same second. After
  every (re)connect, refetch the unread count once to reconcile anything missed while disconnected.
- A connection refused with 401 goes through the same refresh path `baseQueryWithReauth` uses (so the
  refresh mutex still serializes it), then reconnects; when refresh fails, the existing sign-out
  applies. A tenant switch or exit stops and restarts the connection, so it joins the new scope's
  groups.
- Polling becomes the fallback: none while connected; every 60 s with jitter while disconnected; and
  none while the tab is hidden (resume with one refetch when it becomes visible).
- Update the file comment on `use-notification-hub.ts` and the `hooks/` line in `CLAUDE.md`, which
  currently says "polling, not a socket".

### Tests

- Backend integration tests (in the `Notifications` collection, via `NotificationsTestsBase`) that
  connect a real `HubConnection` to the test server's WebSocket client, authenticated with a bearer
  token, and assert:
  - a personal notification reaches its recipient's connection in that scope and not the same account's
    connection in another scope, nor another user's;
  - a tenant-wide notification reaches members of that tenant and no connection acting in another
    tenant or in platform scope; a platform-wide one reaches all of them;
  - a notification raised inside a transaction that rolls back is never pushed;
  - revoking a user's sessions, signing out, and switching tenant each close the old connection;
  - an unauthenticated connection and one whose session was deleted are refused with 401;
  - `unreadCountChanged` reaches the caller's other connection after mark-all-as-read.
- Read-state tests: after mark-all-as-read, a new audience notification is unread and older ones are
  read; mark-as-unread on a row older than the cursor makes it unread and counts it; platform-wide rows
  marked read in a tenant are read in platform scope too; the count never exceeds 100.
- The retention job deletes only rows older than the retention and leaves newer ones in every tenant.
- Vitest for the pure pieces of the web side: the jittered back-off schedule, the capped badge text,
  and the reducer handling of `notificationReceived` / `unreadCountChanged`.

### Documentation

Update the `notifications` skill (read cursor and visit `IsRead`, the hub and its groups, publishing
after commit, `NewUserNotificationsAsync`, the web connection lifecycle, the checklist), the
`background-jobs` skill's list of recurring jobs, `CLAUDE.md` (the notification line under frontend
shared pieces, and the startup guards if any are added), and the same text in
`tool/EasyForNetTool/new-project-claude.md`.

## Out of scope — do not touch

- Other delivery channels: email, browser Web Push, mobile push, per-user notification preferences,
  and toast pop-ups for new notifications.
- The notification list's request and response shapes, its offset paging, and the notification pages'
  layout — beyond the `99+` badge text.
- Which rows a caller may see: `VisibleTo` and the four addressing modes stay exactly as they are.
- Sharing one connection across browser tabs (leader election, `SharedWorker`).
- Sign-in, refresh and tenant-switch flows beyond publishing the ended session id and reconnecting the
  hub. Where refresh tokens and sessions are stored does not change.
- A docker-compose file or any other new root-level file.

## Done when

- `npm run verify` passes, including the new backend and Vitest tests above.
- `GetUnreadCountAsync` issues one SQL statement and never materializes a visit list; after
  mark-all-as-read in a tenant with N audience notifications, the number of visit rows for the caller
  does not grow with N.
- With the API and web app running locally (PostgreSQL + Redis), two browsers signed in as members of
  the same tenant see a tenant-wide notification appear on the badge within a second with no
  `unread-count` request in the network tab between polls; signing one of them out closes its
  WebSocket; a second API instance on another port pushes to a browser connected to the first.
- Deactivating a user from the admin screen closes that user's open WebSocket immediately.
