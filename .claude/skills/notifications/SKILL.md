---
name: notifications
description: Raise an in-app notification from the API and make it render in the web app — INotificationService, user / tenant-wide / platform-wide addressing and the tenant scope each needs, the translation-key contract for title/message, groups and metadata, the SignalR hub each committed notification is pushed through (its groups, publishing after commit, NewUserNotificationsAsync), and the badge. Use when a feature needs to tell users something happened.
---

# In-app notifications

## Raising one

Inject `INotificationService` (from `Backend.Features.Notifications.Core`) and call the method for
the audience:

```csharp
// one account, in the active scope - a tenant, or platform scope
await notificationService.NewUserNotificationAsync(
    userId,
    NotificationType.Warning,
    titleKey: "notifications.inventoryBelowLimit.title",
    messageKey: "notifications.inventoryBelowLimit.message",
    group: "inventory",
    metadata: "{\"itemName\":\"Widget A\",\"currentQty\":5}",
    cancellationToken);

// every member of the active tenant
await notificationService.NewTenantNotificationAsync(NotificationType.Info,
    "notifications.planChanged.title", "notifications.planChanged.message", "system", cancellationToken: cancellationToken);

// every user of the platform, whichever tenant they act in
await notificationService.NewGlobalNotificationAsync(NotificationType.Info,
    "notifications.welcome.title", "notifications.welcome.message", cancellationToken: cancellationToken);
```

`NotificationType` is `Info | Warning | Error | Success` and drives the icon/colour in the UI.

To tell many accounts the same thing, call `NewUserNotificationsAsync(userIds, ...)` rather than looping
over `NewUserNotificationAsync`: one personal row per distinct recipient in the active scope, inserted in
batches of at most 1,000 inside one transaction (the caller's, when one is open), and one push per
recipient.

```csharp
await notificationService.NewUserNotificationsAsync(
    approverIds, NotificationType.Info,
    "notifications.approvalRequested.title", "notifications.approvalRequested.message",
    group: "approvals", cancellationToken: cancellationToken);
```

**Scope.** Attribution comes from `ITenantContext`, never from an argument, and every method throws
`TenantScopeNotEstablishedException` with no scope at all. `NewUser…` stamps the active scope: inside
a tenant the recipient sees the row only while acting in that tenant, and in platform scope the row
names no tenant and the recipient sees it only while acting in platform scope - this is how a platform
account is told something personally. `NewTenant…` needs an actual tenant and throws
`InvalidOperationException` in platform scope, where its row would read as the platform-wide broadcast.
`NewGlobal…` opens platform scope itself and writes `TenantId = null`. From a request the scope is
already set. To address the account in a tenant other than the one the request acts in (a platform
administrator adding a member from platform scope, say) or from a background job, open it first with
`using (tenantContext.BeginTenant(tenantId)) { ... }` (see `background-jobs`, and `multi-tenancy` for
the model), after saving any other pending work, because attribution is stamped at save time.

**Cross-feature use:** `INotificationService` and `NotificationType` are `[AllowOutside]`, so another
slice injects the service directly (`TenantMemberAddEndpoint` tells an account it joined a
tenant). The entities stay private to the slice - never reach for `dbContext.Notifications` from
outside it. See the `backend-feature` skill.

## The translation-key contract

`TitleKey` and `MessageKey` are **i18n keys, not text** — `NotificationItem` renders them with
`t(notification.titleKey)`. So raising a notification always means adding the matching keys to
`src/backend/Source/Features/Localization/Core/Resources/en.json` (and every other shipped resource
file there):

```json
"notifications": {
  "inventoryBelowLimit": { "title": "Low Inventory Alert", "message": "Item is below the minimum stock level" }
}
```

Never pass user-facing English through `titleKey`/`messageKey`, and never interpolate values into
the key — variable parts go in `Metadata`. The web app interpolates the top-level string and number
values of a JSON-object `Metadata` into the text (`notificationVariables` in `lib/utils`), so a
message `"You have been added to ${tenantName}"` raised with
`JsonSerializer.Serialize(new { tenantName = tenant.Name })` shows the name.

`Group` is a short lowercase slug used to filter the list; add its label under
`notifications.groups.<slug>` (the UI falls back to the raw slug). `Metadata` is an opaque JSON
string for details the UI may show; keep it small and stable.

## Addressing and read state

| Row | `TenantId` | `UserId` | Read state |
| --- | --- | --- | --- |
| user, in a tenant | active tenant | recipient | `IsRead` on the row |
| user, in platform scope | null | recipient | `IsRead` on the row |
| tenant-wide | active tenant | null | the user's visit row, else their read cursor for that tenant |
| platform-wide | null | null | the user's visit row, else their platform-wide read cursor |

One audience row is shared by all its readers, so its read state is the reader's, kept in two places:

- **`NotificationReadCursor`** (`UserId`, `TenantId`, `ReadAllAt`) — a user's "mark all as read" point for
  one audience, unique on (`UserId`, `TenantId`) with nulls not distinct: one per tenant, and one
  platform-wide cursor (`TenantId` null). An audience row is read when the cursor for its audience was set
  at or after its `CreatedAt`. Mark-all-as-read moves the cursors; it never writes a row per notification.
- **`NotificationVisit`** (`NotificationId`, `UserId`, `IsRead`), unique on (`NotificationId`, `UserId`) —
  an explicit per-notification state that **overrides the cursor either way**: `IsRead = true` for one
  marked read above the cursor, `IsRead = false` for one marked unread below it. Mark-as-read and
  mark-as-unread upsert it (`INSERT ... ON CONFLICT`); mark-all-as-read deletes the visits its cursors
  cover.

Both belong to the user rather than the scope acted in, so a platform-wide row marked read inside one
tenant reads as read everywhere.

What a caller sees is "rows of the active scope addressed to them or to nobody, plus the platform-wide
rows" — so a personal row never crosses between a tenant and platform scope, and only the platform-wide
broadcast is seen everywhere. That is the internal `VisibleTo(userId, activeTenantId)` in
`Core/NotificationQueries.cs`, applied over `.AcrossAllTenants()` because the tenant filter alone would
hide the platform-wide rows from a caller acting in a tenant. **Whether a visible row is read is decided in
one place too**: `WithReadState(dbContext, userId)` beside it pairs each notification with `IsRead`
(`NotificationWithReadState`), and the unread count, the list's `isRead` filter, its default ordering
(unread first, then newest) and its projection, and `GET {id}` all filter, order and project on that
`.IsRead`. Never restate the rule in a query of your own — chain `.VisibleTo(...).WithReadState(...)`.
Writes go through `SetAudienceReadStateAsync` for one audience row, and the hand-written statements in
mark-all-as-read spell out their tenant predicate, since no query filter reaches them.

**The unread count is capped.** `NotificationQueries.CountUnreadAsync(dbContext, userId, activeTenantId, ct)`
counts at most `UnreadCountCap` (100) rows in a single statement — a count over a `LIMIT`ed subquery,
with no identifiers read into memory — and takes the user and scope as arguments, so it can be computed
for any user in any scope. `INotificationService.GetUnreadCountAsync` calls it for the active scope; the
web badge shows "99+" above 99.

Notifications are `AuditableEntity<Guid>` + `ISoftDelete` + `IMayHaveTenant`; deleting soft-deletes
and the global query filter hides the row. The delete endpoint removes only the caller's own
user-targeted rows — an audience row cannot be deleted by one reader.

## Retention

`Notifications:RetentionDays` (`NotificationOptions`, default 90, must be greater than zero, checked by
`ValidateOnStart`) bounds how long a notification lives. The daily `delete-expired-notifications` job
(`INotificationRetentionService.DeleteExpiredAsync`, registered in `Program.cs`) hard-deletes every
notification whose `CreatedAt` is older than that - read or unread, soft-deleted too - in batches of at most
5,000 rows per statement, and its visit rows go with it through the cascading foreign key. It then prunes the
`IsRead = true` visits on audience notifications that the visiting user's cursor for that audience already
covers (`CreatedAt <= ReadAllAt`); an `IsRead = false` visit, or a read one above the cursor, carries
information and is kept. The job establishes no tenant scope: its hand-written SQL has no tenant predicate
and spans every tenant and platform scope by design, and deletes nothing else. A test of it asserts only on
rows it created, dates "old" rows far past any retention period, and stays in the `Notifications` collection.

## Pushing to connections

`NotificationHub` (`Core/Push`) is mapped at `/hubs/notifications`, outside the API route prefix, behind
the same `Jwt_Or_Cookie` authentication and session validation as every endpoint: the upgrade request is
refused with 401 exactly when an HTTP request with the same credential would be, and with 503
`sessionStoreUnavailable` when the session store is down. A browser authenticates with the auth cookie; a
bearer client passes its token as `?access_token=`, which is read on the hub's path alone
(`NotificationHubRegistration.CarriesQueryAccessToken`) and kept out of the request log. An upgrade
whose `Origin` is not one of the `Web` domains (`WebSetting.AllowedDomains()`, the list the CORS policy
allows) is refused with 403 by the WebSocket middleware (`WebSocketOptions.AllowedOrigins`), so another
host on the same site cannot open the hub with a visitor's cookie; one with no `Origin` - a non-browser
client - is let through. It is **WebSockets only** (long polling and server-sent events are refused; clients skip negotiation),
**server-to-client only** (no client-callable method), and closes when its credential expires.

On connect the hub joins the connection to groups built from the projected session alone - the client
never names one (`NotificationGroups`). SignalR answers the handshake before those joins run, so a
client's start completing does not mean it is in its groups; the account's own `u:` group is joined
last, so a message arriving on it proves the others are joined too:

| Group | Joined by | Receives |
| --- | --- | --- |
| `u:{userId}:{tenantId\|platform}` | every connection, for its account in its scope | personal rows raised in that scope; the caller's `unreadCountChanged` |
| `t:{tenantId}` | connections acting in a tenant | that tenant's tenant-wide rows |
| `all` | every connection | platform-wide rows |

These are the audiences `VisibleTo` reads, so a connection receives exactly the rows its session would
list. `INotificationService` publishes `notificationReceived` - the list row's fields (`id`, `type`,
`titleKey`, `messageKey`, `group`, `metadata`, `createdAt`, `isRead: false`) - to the one group of the
audience, **one message per notification whatever the audience's size**. Mark-as-read, mark-as-unread,
mark-all-as-read and delete publish `unreadCountChanged` (`{ count }`, the capped
`NotificationQueries.CountUnreadAsync`) to the caller's own `u:` group only; nothing is recomputed per
member of an audience.

**Publishing happens after commit.** `INotificationPublisher` (slice-private) sends at once when the
`AppDbContext` has no open transaction, and otherwise hands the send to `NotificationCommitInterceptor`,
a `DbTransactionInterceptor` that runs it when that transaction commits and drops it on rollback, failure,
or disposal without a commit. So raise inside the caller's transaction freely - nothing reaches a
connection for a row that does not exist. Not covered: a `System.Transactions` ambient scope (sent straight
after the save) and a savepoint rollback inside a transaction that then commits. A push that fails is
logged and never fails the raise or the commit. A Hangfire job raises exactly as a request does; the push
goes through the Redis backplane (`ConnectionStrings:Redis`, channels prefixed with `Redis:InstanceName`)
from whichever process runs it. Under `Testing` there is no backplane - pushes stay in process.

**Connection cap.** `Notifications:MaxConnectionsPerUser` (`NotificationOptions`, default 20, validated on
start) caps one account's concurrent connections **per API instance**; the connection over it is closed
with `connectionLimitExceeded` before joining any group, and logged. `NotificationConnectionRegistry` (a
per-instance singleton) tracks open connections by connection, account and session (`OfSession(sid)`).
Hub options: keep-alive and client timeout at SignalR's defaults, a 4 KB `MaximumReceiveMessageSize`
(clients send only the handshake and pings).

**Connection lifecycle.** A connection lives no longer than the session that authenticated it. Identity
announces every ended session id - revocation, sign-out, the session replaced by refresh or tenant
switch/exit - to each `[AllowOutside]` `ISessionEndedHandler`, on every API instance (Redis pub/sub outside
`Testing`, in process under it). `NotificationSessionEndedHandler` aborts each connection
`registry.OfSession(sid)` returns here; the client sees the connection close and reconnects with its
renewed credential, if it has one. A failed announcement is logged and never fails the change that ended
the session. Identity never references this slice. The connection also closes when its access token
expires (`CloseOnAuthenticationExpiration`).

## Endpoints

Under `Features/Notifications/Endpoints/Notifications` with the `notifications` prefix: list
(`GET ""`, filters `isRead` and `group`), `GET {id}`, `DELETE {id}`, `POST {id}/mark-as-read`,
`POST {id}/mark-as-unread`, `POST mark-all-as-read`, `GET unread-count`, `GET groups`. There is no
create endpoint — notifications are raised by server code. They are authenticated but carry **no
permission** — every signed-in user sees their own notifications. Follow that when adding one. An
ordinary account whose tenant was dropped at token renewal acts in platform scope and is answered
with the platform-wide broadcasts alone; that is harmless, and the web app sends it to
`/select-tenant` rather than connecting to the hub.

## Web side

- `notificationsApi` (`store/api/notifications/notifications`) uses the `Notifications` tag type;
  mutations invalidate the collection plus the touched row. `notificationGetUnreadCount` provides
  `{ type: 'Notifications', id: 'UNREAD_COUNT' }`, so every mutation refetches the badge at once;
  a notification mutation must keep invalidating the `Notifications` type, or while the hub is down the
  badge waits for the fallback poll. Every list (and the group list) also provides
  `NOTIFICATIONS_LIST_TAG` (`{ type: 'Notifications', id: 'LIST' }`), which a push invalidates without
  touching the unread count.
- `useNotificationHub()` (`hooks/`) is mounted once, in `components/layouts/header.tsx` - do not mount it
  per screen. It holds **one** SignalR connection (`@microsoft/signalr`) to the hub on the API host
  (`lib/notifications/hub-url.ts` drops the API route prefix), WebSockets only with negotiation skipped,
  authenticated by the auth cookie - no token in the URL, so the web origin must be one of the `Web`
  domains and the cookie must reach the API host. It connects only while the caller has an active tenant
  or is a platform account acting in none (anyone else is on `/select-tenant`), and stops on sign-out,
  when that stops holding, and on unmount; a tenant switch or exit stops it and opens a new one, since
  groups are joined on connect.
- Messages: `notificationReceived` dispatches `notificationsSlice.notificationReceived` (one more unread)
  and invalidates `NOTIFICATIONS_LIST_TAG`; `unreadCountChanged` dispatches `setUnreadCount(count)`. The
  unread count is fetched once after every connect and reconnect, since pushes sent while it was down
  are lost.
- Reconnect: `withAutomaticReconnect` with `reconnectDelayMs` (`lib/notifications/hub-reconnect.ts`) -
  exponential back-off with jitter, capped at 60 s, never giving up. A failed first start, or a close the
  server does not let reconnect, is retried by the hook on the same schedule.
- 401: a refused WebSocket upgrade shows the browser no status, so every failed attempt to connect (a
  start, or one of automatic reconnect's attempts) is answered by one unread-count fetch through RTK Query.
  It carries the same cookie, so it answers 401 exactly when the upgrade was refused for authentication,
  and `baseQueryWithReauth` then refreshes the session under its mutex - or signs out, which stops the
  connection. A connection merely being lost is not probed, only a failed attempt. Sign-out closes the
  connection server-side at once, so a probe then would find no session to refresh: `leaveSignedOut`
  marks the navigation to sign-in first (`store/signed-out-navigation.ts`), after which the hook probes
  nothing and a failed refresh redirects nowhere. Never refresh from the hook by any other route.
- Fallback polling: none while connected; while disconnected, the unread count every
  `fallbackPollDelayMs` (60 s, give or take 10%), paused while the tab is hidden, with one fetch when it
  becomes visible again.
- `components/notifications/` holds `NotificationBell` (the badge, whose text is
  `formatUnreadBadge(count)`: no badge at zero or below, `99+` above 99), `NotificationPanel` (dropdown
  list) and `NotificationItem` (single row, renders `t(titleKey, notificationVariables(metadata))` and the
  same for `messageKey`), with full pages under `app/[lang]/admin/notifications/` (`list`, `[id]`).

## Testing

Derive from `NotificationsTestsBase` (it carries `[Collection("Notifications")]`, because
platform-wide rows reach every account) and use its `CreateUserNotificationAsync`,
`CreateTenantNotificationAsync`, `CreatePlatformUserNotificationAsync`, `CreateGlobalNotificationAsync`
helpers. Assert an audience row's read state through the API — `IsReadForAsync(client, id)` and
`UnreadCountAsync(client)` — because a row the read cursor covers has no visit row at all;
`IsVisitedAsync` (a read visit row exists) and `VisitCountAsync` are for asserting what was stored.
Anything that depends on unread state belongs to a user and tenant the test creates. `NotificationPlatformScopeTests`
shows asking one platform account from both sides of the boundary: `ClientForAsync(username)` signs it in
to platform scope, `ClientForAsync(username, tenantId)` to its tenant.

Hub tests (`Tests/Features/Notifications/Core/NotificationHubTests`) open real connections with
`HubProbe.ConnectAsync(App.Server, accessToken, userId, tenantId)` - WebSockets over the test server,
negotiation skipped, the token as `access_token`, returning only once a sentinel sent to the connection's
`u:` group has arrived (so every group is joined) - and wait for a message with
`WaitForNotificationAsync(predicate)` / `WaitForUnreadCountAsync()`. Show that a message did **not** arrive
with `probe.DrainAsync(sender)` first: it sends a sentinel to the connection's own group, and messages to
one connection arrive in order, so once the sentinel is in, an earlier push would be too - never sleep.
Sentinels are kept out of `Notifications` and `WaitForNotificationAsync`. A connection expected to be
refused is built with `HubProbe.Create` and started by the test, never through `ConnectAsync`. The
test server's WebSocket client bypasses the middleware's origin check, so that is tested by driving
`WebSocketMiddleware` directly. Always dispose probes (`await using`).

## Checklist

- [ ] Called `NewUserNotificationAsync` / `NewTenantNotificationAsync` / `NewGlobalNotificationAsync` with key strings, not text
- [ ] The scope the recipient will read it in is active: their tenant, or platform scope for a platform account (jobs and cross-tenant callers open one explicitly)
- [ ] `notifications.<name>.title` and `.message` added to every backend resource file
- [ ] `notifications.groups.<slug>` added if a new group was introduced
- [ ] Any new endpoint reads through `AcrossAllTenants().VisibleTo(...)`, and takes read state from `.WithReadState(...)` rather than restating the visit / cursor rule
- [ ] An audience row's read state is written by upserting the visit row (`SetAudienceReadStateAsync`), never by inserting one visit per notification in bulk
- [ ] Unread counts go through `NotificationQueries.CountUnreadAsync` and stay capped at `UnreadCountCap`
- [ ] Many recipients of the same notice go through `NewUserNotificationsAsync`, not a loop
- [ ] New notification rows are written through `INotificationService` (which publishes), never added to `dbContext.Notifications` directly
- [ ] An endpoint that changes the caller's read state calls `INotificationPublisher.PublishUnreadCountAsync(userId, activeTenantId, ...)` after its change, and publishes to nobody else
- [ ] A push is published through `INotificationPublisher` (after commit, failures logged), never through `IHubContext` directly
- [ ] Hub groups come from the session alone; no client-callable hub method is added
