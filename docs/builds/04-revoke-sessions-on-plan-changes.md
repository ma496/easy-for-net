# Revoke a tenant's live sessions when its edition or feature values change

| | |
|---|---|
| **Commit** | `61f560a2` |
| **Landed** | 2026-09-29 |
| **Task brief** | `04-revoke-sessions-on-plan-changes.md` |
| **Verification** | `npm run verify` — the static gate, plus whichever live checks the diff demanded |

## What changed

| File | + | − |
|------|---|---|
| `.agent-queue/{todo => doing}/04-revoke-sessions-on-plan-changes.md` | 0 | 0 |
| `.agent-queue/{doing => done}/03-revoke-sessions-on-tenant-membership-and-lifecycle.md` | 0 | 0 |
| `.claude/memory/lessons/never-change-a-seeded-tenant-s-plan-members-or-lifecycle-in-.md` | 9 | 0 |
| `docs/builds/03-revoke-sessions-on-tenant-membership-and-lifecycle.md` | 70 | 0 |
| `docs/builds/README.md` | 2 | 1 |
| `src/backend/Source/Features/Tenancy/Core/EditionService.cs` | 18 | 0 |
| `src/backend/Source/Features/Tenancy/Endpoints/Editions/EditionDeleteEndpoint.cs` | 16 | 1 |
| `src/backend/Source/Features/Tenancy/Endpoints/Editions/EditionUpdateEndpoint.cs` | 3 | 1 |
| `src/backend/Source/Features/Tenancy/Endpoints/FeatureValues/FeatureValueUpdateEndpoint.cs` | 50 | 7 |
| `src/backend/Source/Features/Tenancy/Endpoints/Tenants/TenantUpdateEndpoint.cs` | 22 | 3 |
| `src/backend/Tests/FeatureManagement/FeatureTestsBase.cs` | 4 | 45 |
| `src/backend/Tests/FeatureManagement/PlanChangeSessionRevocationTests.cs` | 293 | 0 |
| `src/backend/Tests/FeatureManagement/SessionFeatureGatingTests.cs` | 27 | 10 |
| `src/backend/Tests/Features/Tenancy/Core/TenantSeedingTests.cs` | 3 | 0 |
| `src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantUpdateTests.cs` | 30 | 20 |
| `src/backend/Tests/Features/Tenancy/TenancyTestsBase.cs` | 44 | 0 |

## The brief this was built from

The task file is reproduced verbatim below. It is the most complete statement of intent
this change has: what to build, what to leave alone, and how anyone could tell it worked.

---

Depends-on: 02-revoke-sessions-on-identity-access-changes

Plan gating (`IPermissionFeatureFilter`) is applied when a session is created. For that to hold, a plan change has to revoke the sessions it affects, so users come back under the new plan. This task makes the tenant, feature-value and edition endpoints revoke through the `[AllowOutside]` interface from task 02, after the change commits.

## Scope
- What each endpoint revokes:
  - `TenantUpdateEndpoint`, when the tenant's edition changes: every session in that tenant. Other updates, such as a rename, revoke nothing.
  - `FeatureValueUpdateEndpoint`, when a tenant's feature values change: every session in that tenant.
  - `EditionUpdateEndpoint`, when the edition's feature values change: every session in every tenant on that edition.
  - `EditionDeleteEndpoint`: every session in every tenant on that edition.
- Rewrite `SessionFeatureGatingTests.A_Session_Already_Issued_Keeps_What_It_Was_Issued_With` rather than deleting it. It now asserts that the plan change revokes a session already issued, which answers 401, and that the next session's permissions reflect the new plan.
- Add an integration test for each case. Each test:
  1. Signs in a session.
  2. Has a platform administrator make the change.
  3. Shows that the old access token, not renewed, answers 401.
  4. Shows that its refresh token is refused.
  5. Shows that tenants the change does not name keep their sessions. For example, a tenant on another edition stays signed in when one edition changes.

## Out of scope — do not touch
- The membership and lifecycle endpoints and their tests, which task 03 owns: `TenantMemberRemoveEndpoint`, `TenantMemberUpdateRolesEndpoint`, `TenantSuspendEndpoint`, `TenantDeleteEndpoint`, `TenantSuspensionTests`, `TenantSwitchTests` and `TenantMemberUpdateRolesTests`.
- The feature value resolution chain, `IFeatureChecker`, and `IPermissionFeatureFilter` with its three callers. This task only adds revocation.
- `DataSeeder` and the `FeatureManagement` configuration section. A configuration change revokes nothing.
- Docs and skills (task 06).

## Done when
- `npm run verify` passes.
- Each case has a test proving that the old token answers 401 without renewal, that its refresh is refused, and that tenants the change does not name keep their sessions.
- `ChangePermissionsWithHiddenGrantsTests`, `FeatureValueEndpointTests`, `EditionEndpointTests` and `UserSeatLimitTests` still pass.
- Plan-hidden grants still carry through a role replacement.
- Turning a feature back on still restores its permissions with nothing to re-grant; they now appear from the next sign-in.
- A plan change never narrows or revokes a session in platform scope.
