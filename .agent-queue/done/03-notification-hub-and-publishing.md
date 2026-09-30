Push new notifications to the right connections over a SignalR hub
Depends-on: 01-notification-read-cursor-and-indexes, 02-notification-retention-job

Today the web app polls for notifications. This task adds a server-to-client `NotificationHub` scaled out through the Redis the API already requires, joins each connection to groups derived from its session, and publishes each committed notification — and each change to the caller's read state — to exactly the connections that may see it.

## Scope
- `NotificationHub` at `/hubs/notifications`, mapped in `Program.cs` behind the same `Jwt_Or_Cookie` authentication and `SessionPrincipalValidator`: refused with 401 exactly when an HTTP request with the same credentials would be, and 503 `sessionStoreUnavailable` when the store is down. Browsers use the auth cookie; a bearer client passes `access_token` in the query string, accepted **only** on the hub path, never logged, and making the policy scheme choose JWT bearer for that request.
- Server-to-client only — no client-callable methods. On connect, add the connection to groups built from the projected session alone: `u:{userId}:{tenantId|platform}`, `t:{tenantId}` when acting in a tenant, and `all`. The client never names a group.
- WebSockets only (long polling and SSE disabled on the server); clients skip negotiation.
- Outside `Testing`: Redis backplane on `ConnectionStrings:Redis`, channel prefix from `Redis:InstanceName`. Under `Testing`: in-process, no backplane — the suite needs PostgreSQL only.
- `HubOptions`: keep-alive and client timeout at SignalR's defaults unless a reason is written beside a change; small `MaximumReceiveMessageSize`; `CloseOnAuthenticationExpiration = true`.
- `Notifications:MaxConnectionsPerUser` (default 20) on the `NotificationOptions` task 02 created: a per-account cap on concurrent connections per API instance; a connection over the cap is refused and logged. Track connections per instance in a way task 04 can extend to look them up by `sid`.
- `INotificationService` keeps its three methods and their signatures. After a notification is saved it publishes `notificationReceived` with the list DTO's fields (`id`, `type`, `titleKey`, `messageKey`, `group`, `metadata`, `createdAt`, `isRead: false`) to the matching group — one message per notification, whatever the audience size.
- Publish only after commit: when the caller's `AppDbContext` has an open transaction, defer until it commits and drop it on rollback (a `DbTransactionInterceptor` or equivalent). A publish failure is logged and never fails the raise.
- New `NewUserNotificationsAsync(IReadOnlyCollection<Guid> userIds, …)` on `INotificationService`: one batched insert in chunks of at most 1,000, one publish per recipient group, in the active scope.
- Mark-as-read, mark-as-unread, mark-all-as-read and delete publish `unreadCountChanged` with the caller's recomputed, capped count (task 01's computation) to `u:{userId}:{scope}` only. Nothing is recomputed per member for a broadcast.
- Raising from a Hangfire job keeps working unchanged and publishes through the backplane from whichever process runs the job.
- Integration tests in the `Notifications` collection via `NotificationsTestsBase`, using a real `HubConnection` over the test server's WebSocket client with a bearer token:
  - a personal row reaches its recipient in that scope, and not the same account's connection in another scope nor another user's;
  - a tenant-wide row reaches that tenant's members and no connection acting in another tenant or in platform scope; a platform-wide row reaches all of them;
  - a notification raised in a transaction that rolls back is never pushed;
  - an unauthenticated connection, and one whose session was deleted, are refused with 401;
  - `unreadCountChanged` reaches the caller's other connection after mark-all-as-read;
  - `NewUserNotificationsAsync` reaches each recipient;
  - the connection over the cap is refused.
- Update the `notifications` skill (the hub and its groups, publishing after commit, `NewUserNotificationsAsync`, the checklist) and, in `CLAUDE.md` and `tool/EasyForNetTool/new-project-claude.md`, any startup guard or option this adds.

## Out of scope — do not touch
- Identity's session code (`ISessionStore` and its stores, `SessionRevocationService`, `SessionIssuer`, `SignoutEndpoint`, `TokenService`) and closing connections when a session ends — task 04 owns that.
- The read-state rule in `NotificationQueries.cs` and the migration (task 01); the retention job and `RetentionDays` (task 02) — extend `NotificationOptions`, do not change what is already there.
- `VisibleTo` and the four addressing modes; the list's request and response shapes.
- The web app, including `use-notification-hub.ts` and the `hooks/` line in `CLAUDE.md` (task 05).
- Any new root-level file (no docker-compose).

## Done when
- `npm run verify` passes, and the suite still needs only PostgreSQL.
- `FeatureDependencyTests` passes.
- A hub connection receives exactly the rows `VisibleTo` would list for its session, and nothing from another tenant or scope.
- Every existing caller of `INotificationService` compiles unchanged, and a raise whose push fails still saves the notification.
- The hub refuses a long-polling or SSE transport, and an `access_token` query string is ignored on every path but the hub's.
- Sign-in, refresh, per-request session validation and 503 `sessionStoreUnavailable` behave on HTTP endpoints exactly as before, for bearer and cookie clients alike.
