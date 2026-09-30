# Build record

Every change that has landed through the task queue, newest first. One file per
build, each preserving the brief the change was built from.

These are written automatically — `scripts/record-build.mjs` runs as each task lands, so
this directory cannot drift from what was actually committed. Do not hand-edit a record;
correct the code that produced it.

To read the change itself: `git show <commit>`.

| Landed | Build | Commit |
|--------|-------|--------|
| 2026-09-30 | [Add the Features/Settings slice with platform and tenant overrides, and move Signin onto it](01-settings-slice-and-signin-setting.md) | `e0b27add` |
| 2026-09-30 | [Move EmailSettings onto the settings system, with configuration defaults and encrypted secrets](02-email-setting-with-configuration-default-and-secrets.md) | `3d3f6ad8` |
| 2026-09-29 | [Keep sessions in a Redis session store and validate them on every request](01-redis-session-store-and-per-request-validation.md) | `af3d2e34` |
| 2026-09-29 | [Revoke live sessions when a user, their password or a role's grants change](02-revoke-sessions-on-identity-access-changes.md) | `5a18f9c7` |
| 2026-09-29 | [Revoke live sessions when a tenant member is removed or re-roled, or the tenant is suspended or deleted](03-revoke-sessions-on-tenant-membership-and-lifecycle.md) | `1a2ed74b` |
| 2026-09-29 | [Revoke a tenant's live sessions when its edition or feature values change](04-revoke-sessions-on-plan-changes.md) | `61f560a2` |
| 2026-09-29 | [Give generated projects their own Redis key prefix and a local Redis setup step](05-generator-redis-instance-name-and-local-redis.md) | `0b6f89c9` |
| 2026-09-29 | [Document that sessions are validated on every request and revoked when access changes](06-document-session-validation-and-revocation.md) | `76c0ba6c` |

8 build(s) recorded.
