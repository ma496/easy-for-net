# Bound notification read state with a per-user read cursor and read-shaped indexes

| | |
|---|---|
| **Commit** | `2a9eb95a` |
| **Landed** | 2026-10-01 |
| **Task brief** | `01-notification-read-cursor-and-indexes.md` |
| **Verification** | `npm run verify` — the static gate, plus whichever live checks the diff demanded |

## What changed

| File | + | − |
|------|---|---|
| `.agent-queue/doing/01-notification-read-cursor-and-indexes.md` | 30 | 0 |
| `.agent-queue/planned.json` | 2 | 1 |
| `.agent-queue/todo/02-notification-retention-job.md` | 22 | 0 |
| `.agent-queue/todo/03-notification-hub-and-publishing.md` | 41 | 0 |
| `.agent-queue/todo/04-close-hub-connections-when-session-ends.md` | 24 | 0 |
| `.agent-queue/todo/05-web-realtime-notifications.md` | 29 | 0 |
| `.claude/skills/notifications/SKILL.md` | 38 | 14 |
| `src/backend/Source/Features/Notifications/Core/Entities/Configuration/NotificationConfiguration.cs` | 16 | 7 |
| `src/backend/Source/Features/Notifications/Core/Entities/Configuration/NotificationReadCursorConfiguration.cs` | 23 | 0 |
| `src/backend/Source/Features/Notifications/Core/Entities/Configuration/NotificationVisitConfiguration.cs` | 4 | 4 |
| `src/backend/Source/Features/Notifications/Core/Entities/NotificationReadCursor.cs` | 24 | 0 |
| `src/backend/Source/Features/Notifications/Core/Entities/NotificationVisit.cs` | 7 | 4 |
| `src/backend/Source/Features/Notifications/Core/NotificationQueries.cs` | 120 | 1 |
| `src/backend/Source/Features/Notifications/Core/NotificationService.cs` | 9 | 21 |
| `src/backend/Source/Features/Notifications/Endpoints/Notifications/NotificationGetEndpoint.cs` | 10 | 12 |
| `src/backend/Source/Features/Notifications/Endpoints/Notifications/NotificationListEndpoint.cs` | 29 | 44 |
| `src/backend/Source/Features/Notifications/Endpoints/Notifications/NotificationMarkAllAsReadEndpoint.cs` | 56 | 25 |
| `src/backend/Source/Features/Notifications/Endpoints/Notifications/NotificationMarkAsReadEndpoint.cs` | 7 | 14 |
| `src/backend/Source/Features/Notifications/Endpoints/Notifications/NotificationMarkAsUnreadEndpoint.cs` | 5 | 4 |
| `src/backend/Source/Migrations/20260930194827_NotificationReadCursor.Designer.cs` | 974 | 0 |
| `src/backend/Source/Migrations/20260930194827_NotificationReadCursor.cs` | 130 | 0 |
| `src/backend/Source/Migrations/AppDbContextModelSnapshot.cs` | 37 | 7 |
| `src/backend/Source/ShareData/AppDbContext.cs` | 1 | 0 |
| `src/backend/Tests/Features/Notifications/Endpoints/Notifications/NotificationGetUnreadCountTests.cs` | 39 | 0 |
| `src/backend/Tests/Features/Notifications/Endpoints/Notifications/NotificationMarkAllAsReadTests.cs` | 158 | 7 |
| `src/backend/Tests/Features/Notifications/Endpoints/Notifications/NotificationMarkAsUnreadTests.cs` | 47 | 3 |
| `src/backend/Tests/Features/Notifications/Endpoints/Notifications/NotificationPlatformScopeTests.cs` | 41 | 15 |
| `src/backend/Tests/Features/Notifications/Endpoints/Notifications/NotificationsTestsBase.cs` | 52 | 9 |

## The brief this was built from

The task file is reproduced verbatim below. It is the most complete statement of intent
this change has: what to build, what to leave alone, and how anyone could tell it worked.

---

Read state for audience rows (tenant-wide and platform-wide) is one `NotificationVisit` per user per notification, so mark-all-as-read writes one row per notification and grows with the audience; `NotificationService.GetUnreadCountAsync` loads every visit id the user has into memory and sends it back as a `NOT IN` list; and the list runs three correlated visit subqueries per row. This task replaces that with a per-user read cursor plus a visit `IsRead` flag, routes every read-state decision through one query helper, caps the unread count, and reshapes the notification indexes around the actual reads — all in one migration.

## Scope
- New entity `NotificationReadCursor` (`UserId`, `TenantId?`, `ReadAllAt`) in the Notifications slice, with its EF configuration and `DbSet`; unique index on (`UserId`, `TenantId`) with nulls **not** distinct (`TenantId == null` is the platform-wide audience's cursor).
- `NotificationVisit` gains `IsRead`; existing rows migrate as `true`.
- Rule: an audience notification is read for a user when it has a visit row for that user (its `IsRead` decides, whatever `CreatedAt` is), or — with no visit row — when its `CreatedAt` is at or before the user's cursor for its audience (the tenant's cursor for a tenant-wide row, the null cursor for a platform-wide row). Personal rows keep using `Notification.IsRead`, unchanged.
- One internal query helper beside `VisibleTo` in `Core/NotificationQueries.cs` expresses that rule; `GetUnreadCountAsync`, the list's `isRead` filter, its default ordering and its `IsRead` projection, `GET {id}`, and the mark-as-read / mark-as-unread endpoints all go through it. `AcrossAllTenants().VisibleTo(...)` (which rows are visible) is not changed.
- Mark-as-read on an audience row upserts a visit with `IsRead = true`; mark-as-unread upserts one with `IsRead = false` (so a row older than the cursor can be unread again).
- `mark-all-as-read`: the existing bulk `ExecuteUpdate` for the caller's personal rows in the active scope; an upsert of the caller's cursor to now for the active tenant's audience (when acting in one) **and** for the platform-wide audience; then delete the caller's visits the new cursors make redundant (`IsRead = true` on notifications created at or before the cursor). No per-notification insert remains. Platform-wide rows marked read in one scope stay read in every scope, as today.
- `GetUnreadCountAsync` runs as a single SQL statement (no id list materialized) and stops counting at 100 — the response `Count` never exceeds 100. Expose the capped-count computation so a later task can reuse it for a given user and scope.
- Indexes: drop the `TitleKey`, `MessageKey` and `CreatedAt` single-column indexes; add partial indexes — personal `(TenantId, UserId, IsRead, CreatedAt DESC) WHERE UserId IS NOT NULL AND NOT IsDeleted`, audience `(TenantId, CreatedAt DESC) WHERE UserId IS NULL AND NOT IsDeleted`; keep exactly one index on visits covering `(UserId, NotificationId)` (the existing unique one if its column order serves).
- One migration for all of the above.
- Tests (in the `Notifications` collection via `NotificationsTestsBase`): after mark-all-as-read a new audience notification is unread and older ones are read; mark-as-unread on a row older than the cursor makes it unread and it is counted; platform-wide rows marked read in a tenant are read in platform scope too; the count never exceeds 100; after mark-all-as-read with N audience notifications the caller's visit-row count does not grow with N. Existing notification tests keep passing (adjust only where they asserted the old per-notification visit rows).
- Update the `notifications` skill's read-state section (read cursor, visit `IsRead`, the single helper) and its checklist.

## Out of scope — do not touch
- The list endpoint's request/response shapes and offset paging; `VisibleTo` and the four addressing modes.
- `INotificationService`'s public method signatures (task 03 adds to it).
- Retention, `NotificationOptions` and any Hangfire job (task 02).
- SignalR, the hub, publishing, Identity's session code (tasks 03, 04).
- The web app, including the `99+` badge (task 05).

## Done when
- `npm run verify` passes.
- `GetUnreadCountAsync` issues one SQL statement (inspect the logged SQL) and never loads visit ids into memory.
- After mark-all-as-read in a tenant with N audience notifications, the caller's visit rows do not grow with N.
- Personal, tenant-wide and platform-wide notifications still list, count, mark read/unread and delete exactly as before from a user's point of view, in tenant and platform scope.
- The migration applies cleanly to an existing database, and existing visits are read after it.
