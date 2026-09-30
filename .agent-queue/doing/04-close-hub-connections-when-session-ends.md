Close every hub connection when the session that authenticated it ends
Depends-on: 03-notification-hub-and-publishing

A hub connection must not outlive its session. This task makes every deletion of a session record — revocation through `ISessionRevocationService`, sign-out, and the replacement on refresh and tenant switch/exit — close every hub connection authenticated by that `sid`, on every API instance.

## Scope
- A narrow `[AllowOutside]` interface in Identity through which it publishes ended session ids. Identity calls it at every point a session record is deleted: revocation, sign-out, refresh, tenant switch and exit. Transport: Redis pub/sub on `ConnectionStrings:Redis` (channel prefixed with `Redis:InstanceName`) outside `Testing`, in-process under it.
- Notifications subscribes and aborts the matching connections, tracked per instance by `sid` (extending task 03's connection tracking).
- Identity takes no dependency on Notifications; `FeatureDependencyTests` stays green.
- A publish failure is logged and never fails the sign-out, refresh, switch or revocation that triggered it.
- Integration tests (in the `Notifications` collection, real `HubConnection`, bearer token): revoking a user's sessions, signing out, refreshing and switching tenant each close the old connection; a connection on another, un-ended session of the same user stays open.
- Update the `notifications` skill (connection lifecycle) and the **Auth** section of `CLAUDE.md` plus the same text in `tool/EasyForNetTool/new-project-claude.md`: an ended session also closes its hub connections.

## Out of scope — do not touch
- Where sessions and refresh tokens are stored, and the sign-in, refresh and tenant-switch flows beyond publishing the ended session id.
- The hub's groups, authentication, transport, backplane, connection cap and publishing (task 03) — use them, do not reshape them.
- `NotificationQueries.cs`, the migration and the retention job (tasks 01, 02).
- The web app (task 05).

## Done when
- `npm run verify` passes, and the suite still needs only PostgreSQL.
- Every row of the session-revocation table in `CLAUDE.md` still revokes exactly what it did, and now also closes the affected hub connections.
- Deactivating a user from the admin screen closes that user's open WebSocket immediately, including one held on a second API instance on another port.
- Sign-out, refresh and tenant switch/exit still succeed when the ended-session publish fails.
