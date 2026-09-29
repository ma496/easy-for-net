# Build record

Every change that has landed through the task queue, newest first. One file per
build, each preserving the brief the change was built from.

These are written automatically — `scripts/record-build.mjs` runs as each task lands, so
this directory cannot drift from what was actually committed. Do not hand-edit a record;
correct the code that produced it.

To read the change itself: `git show <commit>`.

| Landed | Build | Commit |
|--------|-------|--------|
| 2026-09-29 | [Keep sessions in a Redis session store and validate them on every request](01-redis-session-store-and-per-request-validation.md) | `af3d2e34` |
| 2026-09-29 | [Revoke live sessions when a user, their password or a role's grants change](02-revoke-sessions-on-identity-access-changes.md) | `5a18f9c7` |

2 build(s) recorded.
