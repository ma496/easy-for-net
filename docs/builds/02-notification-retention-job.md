# Delete old notifications and redundant visits with a daily retention job

| | |
|---|---|
| **Commit** | `c54430e2` |
| **Landed** | 2026-10-01 |
| **Task brief** | `02-notification-retention-job.md` |
| **Verification** | `npm run verify` — the static gate, plus whichever live checks the diff demanded |

## What changed

| File | + | − |
|------|---|---|
| `.agent-queue/{todo => doing}/02-notification-retention-job.md` | 0 | 0 |
| `.agent-queue/{doing => done}/01-notification-read-cursor-and-indexes.md` | 0 | 0 |
| `.claude/skills/background-jobs/SKILL.md` | 7 | 0 |
| `.claude/skills/notifications/SKILL.md` | 13 | 0 |
| `CLAUDE.md` | 1 | 1 |
| `docs/builds/01-notification-read-cursor-and-indexes.md` | 77 | 0 |
| `docs/builds/README.md` | 2 | 1 |
| `src/backend/Source/Features/Notifications/Core/NotificationOptions.cs` | 17 | 0 |
| `src/backend/Source/Features/Notifications/Core/NotificationRetentionService.cs` | 89 | 0 |
| `src/backend/Source/Features/Notifications/NotificationsFeature.cs` | 7 | 1 |
| `src/backend/Source/Program.cs` | 2 | 0 |
| `src/backend/Source/appsettings.json` | 3 | 0 |
| `src/backend/Tests/Features/Notifications/Core/NotificationRetentionServiceTests.cs` | 284 | 0 |
| `tool/EasyForNetTool/new-project-claude.md` | 1 | 1 |

## The brief this was built from

The task file is reproduced verbatim below. It is the most complete statement of intent
this change has: what to build, what to leave alone, and how anyone could tell it worked.

---

Depends-on: 01-notification-read-cursor-and-indexes

Nothing is ever deleted from the notifications tables. This task adds a `Notifications:RetentionDays` option and a daily Hangfire recurring job that hard-deletes notifications older than the retention in small batches, and prunes visit rows a read cursor (task 01) has made redundant.

## Scope
- `NotificationOptions` bound from the `Notifications` configuration section: `RetentionDays`, default 90, validated `> 0`, `ValidateOnStart`. Add the section with its default to `appsettings.json`.
- A recurring job registered daily at the end of `Program.cs`, beside the token clean-up jobs. It hard-deletes notifications — soft-deleted or not — whose `CreatedAt` is older than the retention, in batches of at most 5,000 rows per statement (loop until none remain) so no statement holds a long lock; visits cascade.
- The same job deletes visit rows made redundant by a cursor (`IsRead = true` on a notification created at or before the user's cursor for its audience).
- The job establishes no tenant scope and says so in a comment: it works across all tenants by design, through `AcrossAllTenants()` or hand-written SQL with no tenant predicate, and deletes nothing else.
- Tests: the job deletes only rows older than the retention and leaves newer ones in every tenant (and in platform-wide / personal rows); it removes redundant visits and keeps an `IsRead = false` visit.
- Update the `background-jobs` skill's list of recurring jobs and the `notifications` skill (retention); add the option to `CLAUDE.md`'s startup guards line and the same text in `tool/EasyForNetTool/new-project-claude.md`.

## Out of scope — do not touch
- `NotificationQueries.cs`, the read-state helper, the mark endpoints and the migration from task 01 — use them, do not reshape them.
- SignalR, the hub, publishing, `Notifications:MaxConnectionsPerUser` (task 03 adds that to the same options class).
- The web app.

## Done when
- `npm run verify` passes.
- Starting the API with `Notifications:RetentionDays` set to 0 fails at startup.
- The job appears in the Hangfire dashboard as a daily recurring job, and running it removes only old rows, across every tenant.
