# Revoke live sessions when a tenant member is removed or re-roled, or the tenant is suspended or deleted

| | |
|---|---|
| **Commit** | `1a2ed74b` |
| **Landed** | 2026-09-29 |
| **Task brief** | `03-revoke-sessions-on-tenant-membership-and-lifecycle.md` |
| **Verification** | `npm run verify` — the static gate, plus whichever live checks the diff demanded |

## What changed

| File | + | − |
|------|---|---|
| `.agent-queue/{todo => doing}/03-revoke-sessions-on-tenant-membership-and-lifecycle.md` | 0 | 0 |
| `.agent-queue/{doing => done}/02-revoke-sessions-on-identity-access-changes.md` | 0 | 0 |
| `.claude/memory/lessons/never-run-backend-tests-while-another-run-is-using-the-test-.md` | 9 | 0 |
| `docs/builds/02-revoke-sessions-on-identity-access-changes.md` | 98 | 0 |
| `docs/builds/README.md` | 2 | 1 |
| `src/backend/Source/Features/Tenancy/Endpoints/Tenants/TenantDeleteEndpoint.cs` | 6 | 5 |
| `src/backend/Source/Features/Tenancy/Endpoints/Tenants/TenantMemberRemoveEndpoint.cs` | 11 | 6 |
| `src/backend/Source/Features/Tenancy/Endpoints/Tenants/TenantMemberUpdateRolesEndpoint.cs` | 9 | 4 |
| `src/backend/Source/Features/Tenancy/Endpoints/Tenants/TenantSuspendEndpoint.cs` | 9 | 7 |
| `src/backend/Tests/Features/FileManagement/Endpoints/Files/FileGetTests.cs` | 13 | 9 |
| `src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantMemberRemoveTests.cs` | 8 | 24 |
| `src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantMemberUpdateRolesTests.cs` | 10 | 18 |
| `src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantSessionRevocationTests.cs` | 256 | 0 |
| `src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantSuspensionTests.cs` | 63 | 111 |
| `src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantSwitchTests.cs` | 67 | 10 |

## The brief this was built from

The task file is reproduced verbatim below. It is the most complete statement of intent
this change has: what to build, what to leave alone, and how anyone could tell it worked.

---

Depends-on: 02-revoke-sessions-on-identity-access-changes

Today, membership and lifecycle changes in the Tenancy slice leave the affected sessions working until they are next renewed. This task ends those sessions at once. Each change calls the `[AllowOutside]` revocation interface from task 02, after the database change commits.

## Scope
- What each endpoint revokes:
  - `TenantMemberRemoveEndpoint`: that user's sessions in that tenant.
  - `TenantMemberUpdateRolesEndpoint`: that user's sessions in that tenant.
  - `TenantSuspendEndpoint`: every session in that tenant.
  - `TenantDeleteEndpoint`: every session in that tenant.
- Revocation runs only after the change commits. The Tenancy slice reaches revocation only through the `[AllowOutside]` interface, never through `ISessionStore` or Identity entities.
- Rewrite `TenantSuspensionTests`, `TenantSwitchTests` and `TenantMemberUpdateRolesTests` so they no longer need `RenewAsync()` to see a change: the old token answers 401 at once. Keep the assertions about renewal behaviour that are still true.
- Add an integration test for each endpoint. Each test:
  1. Signs in a session.
  2. Has an administrator make the change.
  3. Shows that the old access token, not renewed, answers 401.
  4. Shows that its refresh token is refused.
  5. Shows that sessions the change does not name keep working. Removing a user from tenant A leaves their tenant B session alive. Suspending tenant A leaves tenant B's users, and sessions in platform scope, alone.

## Out of scope — do not touch
- `TenantUpdateEndpoint`, `FeatureValueUpdateEndpoint`, `EditionUpdateEndpoint`, `EditionDeleteEndpoint` and `SessionFeatureGatingTests`. Task 04 owns them.
- The Identity endpoints and the revocation service from task 02. The store and validation from task 01.
- Sign-in and switch rules (which tenant a session enters, `tenantRequired`), and the web app's switch and refresh flows.
- Docs and skills (task 06).

## Done when
- `npm run verify` passes.
- `TenantSuspensionTests`, `TenantSwitchTests` and `TenantMemberUpdateRolesTests` make no `RenewAsync()` call just to observe a change.
- Each endpoint has a test proving that the old token answers 401 without renewal, that the refresh is refused, and that sessions the change does not name survive.
- Seat counting (`GetSeatsAsync`) and the rules for platform accounts as tenant members behave as before.
- By hand, with Redis and the web app running:
  1. Sign in as a tenant user in one browser.
  2. In another browser, sign in as `tenantadmin` and remove that user from the tenant.
  3. The first browser's next navigation lands on `/signin`.
