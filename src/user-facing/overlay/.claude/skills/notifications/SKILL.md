---
name: notifications
description: Raise an in-app notification from the API and make it render in the web app — INotificationService, one-account and all-users addressing, the translation-key contract for title/message, groups and metadata, read state (visits and the read cursor), the SignalR hub each committed notification is pushed through (its groups, publishing after commit, NewUserNotificationsAsync), and the badge in the admin shell. Use when a feature needs to tell users something happened.
---

# In-app notifications

## Raising one

Inject `INotificationService` (from `Backend.Features.Notifications.Core`) and call the method for
the audience:

```csharp
// one account
await notificationService.NewUserNotificationAsync(
    userId,
    NotificationType.Warning,
    titleKey: "notifications.inventoryBelowLimit.title",
    messageKey: "notifications.inventoryBelowLimit.message",
    group: "inventory",
    metadata: "{\"itemName\":\"Widget A\",\"currentQty\":5}",
    cancellationToken);

// every user
await notificationService.NewAllUsersNotificationAsync(NotificationType.Info,
    "notifications.welcome.title", "notifications.welcome.message", "system", cancellationToken: cancellationToken);
```

`NotificationType` is `Info | Warning | Error | Success` and drives the icon/colour in the UI.

To tell many accounts the same thing, call `NewUserNotificationsAsync(userIds, ...)` rather than looping
over `NewUserNotificationAsync`: one personal row per distinct recipient, inserted in batches of at most
1,000 inside one transaction (the caller's, when one is open), and one push per recipient.

```csharp
await notificationService.NewUserNotificationsAsync(
    approverIds, NotificationType.Info,
    "notifications.approvalRequested.title", "notifications.approvalRequested.message",
    group: "approvals", cancellationToken: cancellationToken);
```

The recipient is the argument and nothing else — there is no ambient scope to establish, so a request
and a Hangfire job raise exactly the same way.

**Cross-feature use:** `INotificationService` and `NotificationType` are `[AllowOutside]`, so another
slice injects the service directly. The entities stay private to the slice — never reach for
`dbContext.Notifications` from outside it. See the `backend-feature` skill.

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
message `"Order ${orderId} has shipped"` raised with
`JsonSerializer.Serialize(new { orderId = order.Number })` shows the number.

`Group` is a short lowercase slug used to filter the list; add its label under
`notifications.groups.<slug>` (the UI falls back to the raw slug). `Metadata` is an opaque JSON
string for details the UI may show; keep it small and stable.

## Addressing and read state

| Row | `UserId` | Read state |
| --- | --- | --- |
| one account | recipient | `IsRead` on the row |
| all users (broadcast) | null | the user's visit row, else their read cursor |

A broadcast row is shared by all its readers, so its read state is the reader's, kept in two places:

- **`NotificationReadCursor`** (`UserId`, `ReadAllAt`) — a user's "mark all as read" point, unique on
  `UserId`. A broadcast is read when the cursor was set at or after its `CreatedAt`. Mark-all-as-read
  moves the cursor; it never writes a row per notification.
- **`NotificationVisit`** (`NotificationId`, `UserId`, `IsRead`), unique on (`NotificationId`, `UserId`) —
  an explicit per-notification state that **overrides the cursor either way**: `IsRead = true` for one
  marked read above the cursor, `IsRead = false` for one marked unread below it. Mark-as-read and
  mark-as-unread upsert it (`INSERT ... ON CONFLICT`); mark-all-as-read deletes the visits its cursor
  covers.

What a caller sees is "rows addressed to them or to nobody" — the internal `VisibleTo(userId)` in
`Core/NotificationQueries.cs`. **Whether a visible row is read is decided in one place too**:
`WithReadState(dbContext, userId)` beside it pairs each notification with `IsRead`
(`NotificationWithReadState`), and the unread count, the list's `isRead` filter, its default ordering
(unread first, then newest) and its projection, and `GET {id}` all filter, order and project on that
`.IsRead`. Never restate the rule in a query of your own — chain `.VisibleTo(...).WithReadState(...)`.
Writes go through `SetAudienceReadStateAsync` for one broadcast row.

**The unread count is capped.** `NotificationQueries.CountUnreadAsync(dbContext, userId, ct)` counts at
most `UnreadCountCap` (100) rows in a single statement — a count over a `LIMIT`ed subquery, with no
identifiers read into memory. `INotificationService.GetUnreadCountAsync` calls it; the web badge shows
"99+" above 99.

Notifications are `AuditableEntity<Guid>` + `ISoftDelete`; deleting soft-deletes and the global query
filter hides the row. The delete endpoint removes only the caller's own user-targeted rows — a broadcast
cannot be deleted by one reader.

## Retention

`Notifications:RetentionDays` (`NotificationOptions`, default 90, must be greater than zero, checked by
`ValidateOnStart`) bounds how long a notification lives. The daily `delete-expired-notifications` job
(`INotificationRetentionService.DeleteExpiredAsync`, registered in `Program.cs`) hard-deletes every
notification whose `CreatedAt` is older than that — read or unread, soft-deleted too — in batches of at
most 5,000 rows per statement (`BatchSize`), and its visit rows go with it through the cascading foreign
key. It then prunes the `IsRead = true` visits on broadcasts that the visiting user's cursor already
covers (`CreatedAt <= ReadAllAt`); an `IsRead = false` visit, or a read one above the cursor, carries
information and is kept. Its statements are hand-written, so no query filter reaches them, and it
deletes nothing else. A test of it asserts only on rows it created, dates "old" rows far past any
retention period, and stays in the `Notifications` collection.

## Pushing to connections

`NotificationHub` (`Core/Push`) is mapped at `/hubs/notifications`, outside the API route prefix, behind
the same `Jwt_Or_Cookie` authentication and session validation as every endpoint: the upgrade request is
refused with 401 exactly when an HTTP request with the same credential would be, and with 503
`sessionStoreUnavailable` when the session store is down. A browser authenticates with the auth cookie; a
bearer client passes its token as `?access_token=`, which is read on the hub's path alone
(`NotificationHubRegistration.CarriesQueryAccessToken`) and kept out of the request log. An upgrade
whose `Origin` is neither the request's own origin (scheme and host) nor one of the `Web` domains
(`WebSetting.AllowedDomains()`, the list the CORS policy allows) is refused with 403 by
`NotificationHubOriginMiddleware`, so another site's page cannot open the hub with a visitor's cookie;
one with no `Origin` — a non-browser client — is let through. It is **WebSockets only** (long polling
and server-sent events are refused; clients skip negotiation), **server-to-client only** (no
client-callable method), and closes when its credential expires.

On connect the hub joins the connection to groups built from the projected session alone — the client
never names one (`NotificationGroups`). SignalR answers the handshake before those joins run, so a
client's start completing does not mean it is in its groups; the account's own `u:` group is joined
last, so a message arriving on it proves the other is joined too:

| Group | Joined by | Receives |
| --- | --- | --- |
| `u:{userId}` | every connection, for its account | personal rows; the caller's `unreadCountChanged` |
| `all` | every connection | all-users broadcasts |

These are the audiences `VisibleTo` reads, so a connection receives exactly the rows its session would
list. `INotificationService` publishes `notificationReceived` — the list row's fields (`id`, `type`,
`titleKey`, `messageKey`, `group`, `metadata`, `createdAt`, `isRead: false`) — to the one group of the
audience, **one message per notification whatever the audience's size**. Mark-as-read, mark-as-unread,
mark-all-as-read and delete publish `unreadCountChanged` (`{ count }`, the capped
`NotificationQueries.CountUnreadAsync`) to the caller's own `u:` group only
(`INotificationPublisher.PublishUnreadCountAsync(userId, ct)`); nothing is recomputed per reader of a
broadcast.

**Publishing happens after commit.** `INotificationPublisher` (slice-private) sends at once when the
`AppDbContext` has no open transaction, and otherwise hands the send to `NotificationCommitInterceptor`,
a `DbTransactionInterceptor` that runs it when that transaction commits and drops it on rollback, failure,
or disposal without a commit. So raise inside the caller's transaction freely — nothing reaches a
connection for a row that does not exist. Not covered: a `System.Transactions` ambient scope (sent straight
after the save) and a savepoint rollback inside a transaction that then commits. A push that fails is
logged and never fails the raise or the commit. A Hangfire job raises exactly as a request does; the push
goes through the Redis backplane (`ConnectionStrings:Redis`, channels prefixed with `Redis:InstanceName`)
from whichever process runs it. Under `Testing` there is no backplane — pushes stay in process.

**Connection cap.** `Notifications:MaxConnectionsPerUser` (`NotificationOptions`, default 20, validated on
start) caps one account's concurrent connections **per API instance**; the connection over it is refused
with `connectionLimitExceeded` before joining any group, and logged. `NotificationConnectionRegistry` (a
per-instance singleton) tracks open connections by connection, account and session (`OfSession(sid)`).
Hub options: keep-alive and client timeout at SignalR's defaults, a 4 KB `MaximumReceiveMessageSize`
(clients send only the handshake and pings).

**Connection lifecycle.** A connection lives no longer than the session that authenticated it. Identity
announces every ended session id — revocation, sign-out, the session replaced by refresh — to each
`[AllowOutside]` `ISessionEndedHandler`, on every API instance (Redis pub/sub outside `Testing`, in
process under it). `NotificationSessionEndedHandler` aborts each connection `registry.OfSession(sid)`
returns here; the client sees the connection close and reconnects with its renewed credential, if it has
one. A failed announcement is logged and never fails the change that ended the session. Identity never
references this slice. The connection also closes when its access token expires
(`CloseOnAuthenticationExpiration`).

## Endpoints

Under `Features/Notifications/Endpoints/Notifications` with the `notifications` prefix: list
(`GET ""`, filters `isRead` and `group`), `GET {id}`, `DELETE {id}`, `POST {id}/mark-as-read`,
`POST {id}/mark-as-unread`, `POST mark-all-as-read`, `GET unread-count`, `GET groups`. There is no
create endpoint — notifications are raised by server code. They are authenticated but carry **no
permission** — every signed-in user, a visitor who signed up included, sees their own notifications.
Follow that when adding one.

## Web side

- `notificationsApi` (`store/api/notifications/notifications`) uses the `Notifications` tag type;
  mutations invalidate the collection plus the touched row. `notificationGetUnreadCount` provides
  `{ type: 'Notifications', id: 'UNREAD_COUNT' }`, so every mutation refetches the badge at once;
  a notification mutation must keep invalidating the `Notifications` type, or while the hub is down the
  badge waits for the fallback poll. Every list (and the group list) also provides
  `NOTIFICATIONS_LIST_TAG` (`{ type: 'Notifications', id: 'LIST' }`), which a push invalidates without
  touching the unread count.
- **Where they show.** The bell (`NotificationBell`) and `useNotificationHub()` live in the admin shell's
  `components/layouts/header.tsx`, and the full pages under `app/[lang]/admin/notifications/` (the list at
  its root, one notification under `[id]`); the admin area needs `Admin.Access`. The public site's
  header (`app/[lang]/(site)/_components/site-header.tsx`) and the account screens show no
  notifications, so a notification meant for a visitor without `Admin.Access` needs a surface of its
  own there (mount the hook once in that shell, never per screen, and never in both shells of one
  document).
- `useNotificationHub()` (`hooks/`) holds **one** SignalR connection (`@microsoft/signalr`) to the hub
  (`lib/notifications/hub-url.ts` drops the API route prefix), WebSockets only with negotiation skipped,
  authenticated by the auth cookie — no token in the URL, so the web origin must be one of the `Web`
  domains or the API's own origin. It connects only while an account is signed in, and stops on
  sign-out and on unmount.
- Messages: `notificationReceived` dispatches `notificationsSlice.notificationReceived` (one more unread)
  and invalidates `NOTIFICATIONS_LIST_TAG`; `unreadCountChanged` dispatches `setUnreadCount(count)`. The
  unread count is fetched once after every connect and reconnect, since pushes sent while it was down
  are lost.
- Reconnect: `withAutomaticReconnect` with `reconnectDelayMs` (`lib/notifications/hub-reconnect.ts`) —
  exponential back-off with jitter, capped at 60 s, never giving up. A failed first start, or a close the
  server does not let reconnect, is retried by the hook on the same schedule.
- 401: a refused WebSocket upgrade shows the browser no status, so every failed attempt to connect (a
  start, or one of automatic reconnect's attempts) is answered by one unread-count fetch through RTK Query.
  It carries the same cookie, so it answers 401 exactly when the upgrade was refused for authentication,
  and `baseQueryWithReauth` then refreshes the session under its mutex — or signs out, which stops the
  connection. A connection merely being lost is not probed, only a failed attempt. Sign-out closes the
  connection server-side at once, so a probe then would find no session to refresh: `leaveSignedOut`
  (`store/leave-signed-out.ts`) marks the navigation to sign-in first (`markLeavingSignedOut` in
  `store/signed-out-navigation.ts`), after which the hook probes nothing and a failed refresh redirects
  nowhere. Never refresh from the hook by any other route.
- Fallback polling: none while connected; while disconnected, the unread count every
  `fallbackPollDelayMs` (60 s, give or take 10%), paused while the tab is hidden, with one fetch when it
  becomes visible again.
- `components/notifications/` holds `NotificationBell` (the badge, whose text is
  `formatUnreadBadge(count)`: no badge at zero or below, `99+` above 99), `NotificationPanel` (dropdown
  list) and `NotificationItem` (single row, renders `t(titleKey, notificationVariables(metadata))` and the
  same for `messageKey`, linking to `adminPath('/notifications/{id}')`).

## Testing

Derive from `NotificationsTestsBase` (it carries `[Collection("Notifications")]`, because a broadcast
reaches every account) and use its `CreateUserNotificationAsync` and `CreateGlobalNotificationAsync`
helpers. Assert a broadcast's read state through the API — `IsReadForAsync(client, id)` and
`UnreadCountAsync(client)` — because a row the read cursor covers has no visit row at all;
`IsVisitedAsync` (a read visit row exists) and `VisitCountAsync` are for asserting what was stored.
Anything that depends on unread state belongs to a user the test creates.

Hub tests (`Tests/Features/Notifications/Core/NotificationHubTests`) open real connections with
`HubProbe.ConnectAsync(App.Server, accessToken, userId)` — WebSockets over the test server,
negotiation skipped, the token as `access_token`, returning only once a sentinel sent to the connection's
`u:` group has arrived (so every group is joined) — and wait for a message with
`WaitForNotificationAsync(predicate)` / `WaitForUnreadCountAsync()`. Show that a message did **not** arrive
with `probe.DrainAsync(sender)` first: it sends a sentinel to the connection's own group, and messages to
one connection arrive in order, so once the sentinel is in, an earlier push would be too — never sleep.
A connection expected to be refused is built with `HubProbe.Create` (which takes an `origin`) and started
by the test, never through `ConnectAsync`. Always dispose probes (`await using`).

## Checklist

- [ ] Called `NewUserNotificationAsync` / `NewUserNotificationsAsync` / `NewAllUsersNotificationAsync` with key strings, not text
- [ ] `notifications.<name>.title` and `.message` added to every backend resource file
- [ ] `notifications.groups.<slug>` added if a new group was introduced
- [ ] Any new endpoint reads through `.VisibleTo(userId)`, and takes read state from `.WithReadState(...)` rather than restating the visit / cursor rule
- [ ] A broadcast's read state is written by upserting the visit row (`SetAudienceReadStateAsync`), never by inserting one visit per notification in bulk
- [ ] Unread counts go through `NotificationQueries.CountUnreadAsync` and stay capped at `UnreadCountCap`
- [ ] Many recipients of the same notice go through `NewUserNotificationsAsync`, not a loop
- [ ] New notification rows are written through `INotificationService` (which publishes), never added to `dbContext.Notifications` directly
- [ ] An endpoint that changes the caller's read state calls `INotificationPublisher.PublishUnreadCountAsync(userId, ...)` after its change, and publishes to nobody else
- [ ] A push is published through `INotificationPublisher` (after commit, failures logged), never through `IHubContext` directly
- [ ] Hub groups come from the session alone; no client-callable hub method is added
- [ ] A notification meant for visitors has somewhere they can see it outside the admin area
