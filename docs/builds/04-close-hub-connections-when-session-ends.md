# Close every hub connection when the session that authenticated it ends

| | |
|---|---|
| **Commit** | `860db62c` |
| **Landed** | 2026-10-01 |
| **Task brief** | `04-close-hub-connections-when-session-ends.md` |
| **Verification** | `npm run verify` — the static gate, plus whichever live checks the diff demanded |

## What changed

| File | + | − |
|------|---|---|
| `.agent-queue/{todo => doing}/04-close-hub-connections-when-session-ends.md` | 0 | 0 |
| `.agent-queue/{doing => done}/03-notification-hub-and-publishing.md` | 0 | 0 |
| `.claude/skills/notifications/SKILL.md` | 9 | 0 |
| `CLAUDE.md` | 2 | 0 |
| `docs/builds/03-notification-hub-and-publishing.md` | 90 | 0 |
| `docs/builds/README.md` | 2 | 1 |
| `src/backend/Source/Features/Identity/Core/Sessions/ISessionEndedHandler.cs` | 32 | 0 |
| `src/backend/Source/Features/Identity/Core/Sessions/ISessionStore.cs` | 6 | 3 |
| `src/backend/Source/Features/Identity/Core/Sessions/InMemorySessionStore.cs` | 9 | 6 |
| `src/backend/Source/Features/Identity/Core/Sessions/RedisSessionEndedChannel.cs` | 215 | 0 |
| `src/backend/Source/Features/Identity/Core/Sessions/RedisSessionStore.cs` | 9 | 5 |
| `src/backend/Source/Features/Identity/Core/Sessions/SessionEndedPublisher.cs` | 137 | 0 |
| `src/backend/Source/Features/Identity/Core/Sessions/SessionStoreRegistration.cs` | 19 | 3 |
| `src/backend/Source/Features/Notifications/Core/Push/NotificationHubRegistration.cs` | 4 | 0 |
| `src/backend/Source/Features/Notifications/Core/Push/NotificationSessionEndedHandler.cs` | 37 | 0 |
| `src/backend/Tests/Fakes/FaultInjectingSessionStore.cs` | 3 | 3 |
| `src/backend/Tests/Fakes/FaultingSessionEndedHandler.cs` | 35 | 0 |
| `src/backend/Tests/Fakes/TestDoubles.cs` | 9 | 4 |
| `src/backend/Tests/Features/Identity/Sessions/InMemorySessionStoreTests.cs` | 12 | 7 |
| `src/backend/Tests/Features/Identity/Sessions/SessionEndedPublishingTests.cs` | 164 | 0 |
| `src/backend/Tests/Features/Identity/Sessions/SessionTokenTests.cs` | 1 | 0 |
| `src/backend/Tests/Features/Notifications/Core/NotificationSessionEndTests.cs` | 226 | 0 |
| `tool/EasyForNetTool/new-project-claude.md` | 2 | 0 |

## The brief this was built from

The task file is reproduced verbatim below. It is the most complete statement of intent
this change has: what to build, what to leave alone, and how anyone could tell it worked.

---

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
