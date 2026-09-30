Bound notification read state with a per-user read cursor and read-shaped indexes

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
