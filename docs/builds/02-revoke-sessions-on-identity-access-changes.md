# Revoke live sessions when a user, their password or a role's grants change

| | |
|---|---|
| **Commit** | `5a18f9c7` |
| **Landed** | 2026-09-29 |
| **Task brief** | `02-revoke-sessions-on-identity-access-changes.md` |
| **Verification** | `npm run verify` — the static gate, plus whichever live checks the diff demanded |

## What changed

| File | + | − |
|------|---|---|
| `.agent-queue/{todo => doing}/02-revoke-sessions-on-identity-access-changes.md` | 0 | 0 |
| `.agent-queue/{doing => done}/01-redis-session-store-and-per-request-validation.md` | 0 | 0 |
| `.claude/memory/lessons/end-a-session-by-deleting-its-refresh-rows-before-its-store-.md` | 9 | 0 |
| `.claude/memory/lessons/only-platform-scope-can-edit-an-account-shared-across-tenant.md` | 9 | 0 |
| `docs/builds/01-redis-session-store-and-per-request-validation.md` | 139 | 0 |
| `docs/builds/README.md` | 13 | 9 |
| `src/backend/Source/Features/Identity/Core/AuthTokenService.cs` | 74 | 5 |
| `src/backend/Source/Features/Identity/Core/Entities/AuthToken.cs` | 7 | 0 |
| `src/backend/Source/Features/Identity/Core/Entities/Configuration/AuthTokenConfiguration.cs` | 2 | 0 |
| `src/backend/Source/Features/Identity/Core/Entities/User.cs` | 7 | 0 |
| `src/backend/Source/Features/Identity/Core/Sessions/SessionIssuer.cs` | 1 | 0 |
| `src/backend/Source/Features/Identity/Core/Sessions/SessionRecord.cs` | 7 | 0 |
| `src/backend/Source/Features/Identity/Core/Sessions/SessionRevocationService.cs` | 202 | 0 |
| `src/backend/Source/Features/Identity/Core/TenantAuthorizationService.cs` | 24 | 10 |
| `src/backend/Source/Features/Identity/Core/UserService.cs` | 1 | 0 |
| `src/backend/Source/Features/Identity/Endpoints/Account/ChangePasswordEndpoint.cs` | 20 | 1 |
| `src/backend/Source/Features/Identity/Endpoints/Account/ResetPasswordEndpoint.cs` | 9 | 2 |
| `src/backend/Source/Features/Identity/Endpoints/Account/TokenEndpoint.cs` | 1 | 1 |
| `src/backend/Source/Features/Identity/Endpoints/Account/TokenService.cs` | 63 | 6 |
| `src/backend/Source/Features/Identity/Endpoints/Roles/ChangePermissionsEndpoint.cs` | 19 | 1 |
| `src/backend/Source/Features/Identity/Endpoints/Roles/RoleDeleteEndpoint.cs` | 13 | 2 |
| `src/backend/Source/Features/Identity/Endpoints/Roles/RoleUpdateEndpoint.cs` | 4 | 0 |
| `src/backend/Source/Features/Identity/Endpoints/Users/UserDeleteEndpoint.cs` | 6 | 1 |
| `src/backend/Source/Features/Identity/Endpoints/Users/UserUpdateEndpoint.cs` | 27 | 1 |
| `src/backend/Source/Features/Identity/IdentityFeature.cs` | 1 | 0 |
| `src/backend/Source/Migrations/20260929001915_AddSecurityStamp.Designer.cs` | 901 | 0 |
| `src/backend/Source/Migrations/20260929001915_AddSecurityStamp.cs` | 45 | 0 |
| `src/backend/Source/Migrations/20260929005712_AddAuthTokenTenantIdIndex.Designer.cs` | 903 | 0 |
| `src/backend/Source/Migrations/20260929005712_AddAuthTokenTenantIdIndex.cs` | 29 | 0 |
| `src/backend/Source/Migrations/AppDbContextModelSnapshot.cs` | 8 | 0 |
| `src/backend/Tests/Features/Identity/Core/AuthTokenServiceTests.cs` | 3 | 3 |
| `src/backend/Tests/Features/Identity/Endpoints/Account/ChangePasswordTests.cs` | 44 | 18 |
| `src/backend/Tests/Features/Identity/Endpoints/Account/ResetPasswordSessionRevocationTests.cs` | 52 | 0 |
| `src/backend/Tests/Features/Identity/Endpoints/Roles/RoleSessionRevocationTests.cs` | 156 | 0 |
| `src/backend/Tests/Features/Identity/Endpoints/Users/UserSessionRevocationTests.cs` | 231 | 0 |
| `src/backend/Tests/Features/Identity/SessionRevocationTestsBase.cs` | 115 | 0 |
| `src/backend/Tests/Features/Identity/Sessions/SecurityStampTests.cs` | 174 | 0 |
| `src/backend/Tests/Features/Identity/Sessions/SessionRevocationServiceTests.cs` | 163 | 0 |

## The brief this was built from

The task file is reproduced verbatim below. It is the most complete statement of intent
this change has: what to build, what to leave alone, and how anyone could tell it worked.

---

Depends-on: 01-redis-session-store-and-per-request-validation

Task 01 put every session in `ISessionStore` and validates it on each request. This task makes the Identity slice's access changes delete the affected sessions **and** their `AuthToken` refresh rows. The old access token then answers 401 on its next request, and its refresh token is refused. This task also publishes the revocation service that the Tenancy slice calls in tasks 03 and 04.

## Scope
- A revocation service in the Identity slice. It deletes sessions from `ISessionStore` together with the matching `AuthToken` rows. It revokes:
  - every session of a user;
  - a user's sessions in one tenant, or in platform scope;
  - every session in a tenant;
  - the sessions of a set of users in one scope;
  - every session of a user except a given `sid`.
- Expose the revocation service to other slices through an `[AllowOutside]` interface, following the feature-isolation rules. Nothing outside the Identity slice touches `ISessionStore` directly.
- Revocation runs **after** the database change commits, so a rolled-back change revokes nothing.
- What each endpoint revokes:
  - `UserUpdateEndpoint`, when `IsActive` becomes false: every session of that user.
  - `UserDeleteEndpoint`: every session of that user.
  - `UserUpdateEndpoint`, when the user's roles change: that user's sessions in the scope the roles belong to — the tenant, or platform scope for platform roles.
  - `ResetPasswordEndpoint`: every session of that user.
  - `ChangePasswordEndpoint`: every session of that user except the current one.
  - `ChangePermissionsEndpoint`, a `RoleUpdateEndpoint` change that alters what the role grants, and `RoleDeleteEndpoint`: the sessions of every user who holds the role, in the role's scope.
- Rewrite `ChangePasswordTests.ChangePassword_Replaces_The_Credentials_And_Keeps_The_Session` rather than deleting it. It now asserts that the current session survives, while a second session of the same user answers 401 and cannot refresh.
- Add an integration test for each case above. Each test:
  1. Signs in a session.
  2. Has an administrator make the change.
  3. Shows that the **old access token, not renewed**, answers 401 on its next request.
  4. Shows that its refresh token is refused.
  5. Shows that sessions the change does not name keep working. For example, users of another role are unaffected when one role changes, and a user's session in another tenant stays alive when a role in this tenant changes.

## Out of scope — do not touch
- The Tenancy endpoints and their tests: `TenantMember*`, `TenantSuspend*`, `TenantDelete*`, `TenantUpdate*`, `FeatureValue*` and `Edition*`. Tasks 03 and 04 own them.
- What task 01 built: `ISessionStore` and its implementations, the validation step, token minting and `GetInfoEndpoint`. Use them without reshaping them.
- `DataSeeder`'s startup reconciliation, which must revoke nothing.
- Docs and skills (task 06), the generator and the `new-project` skill (task 05).

## Done when
- `npm run verify` passes, and the gate still needs only PostgreSQL.
- Every case above has an integration test showing that the old access token answers 401 without renewal, that its refresh token is refused, and that a session the change does not name keeps working.
- A change whose transaction rolls back leaves the user's sessions working.
- `FeatureDependencyTests` passes, with the revocation interface marked `[AllowOutside]`.
