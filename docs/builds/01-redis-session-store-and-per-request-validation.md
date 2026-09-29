# Keep sessions in a Redis session store and validate them on every request

| | |
|---|---|
| **Commit** | `af3d2e34` |
| **Landed** | 2026-09-29 |
| **Task brief** | `01-redis-session-store-and-per-request-validation.md` |
| **Verification** | `npm run verify` — the static gate, plus whichever live checks the diff demanded |

## What changed

| File | + | − |
|------|---|---|
| `.agent-queue/doing/01-redis-session-store-and-per-request-validation.md` | 58 | 0 |
| `.agent-queue/planned.json` | 3 | 1 |
| `.agent-queue/todo/02-revoke-sessions-on-identity-access-changes.md` | 40 | 0 |
| `.agent-queue/todo/03-revoke-sessions-on-tenant-membership-and-lifecycle.md` | 35 | 0 |
| `.agent-queue/todo/04-revoke-sessions-on-plan-changes.md` | 32 | 0 |
| `.agent-queue/todo/05-generator-redis-instance-name-and-local-redis.md` | 24 | 0 |
| `.agent-queue/todo/06-document-session-validation-and-revocation.md` | 29 | 0 |
| `.claude/memory/lessons/check-for-a-running-backend-exe-before-blaming-the-build.md` | 9 | 0 |
| `.claude/memory/lessons/fastendpoints-jwts-carry-nameidentifier-as-the-full-claim-ur.md` | 9 | 0 |
| `agentic.config.json` | 5 | 0 |
| `scripts/redis-ready.mjs` | 53 | 0 |
| `src/backend/Source/Backend.csproj` | 1 | 0 |
| `src/backend/Source/ErrorHandling/ErrorCodes.cs` | 1 | 0 |
| `src/backend/Source/Exceptions/SessionStoreUnavailableException.cs` | 14 | 0 |
| `src/backend/Source/Features/Identity/Core/AuthTokenService.cs` | 16 | 4 |
| `src/backend/Source/Features/Identity/Core/ClaimConstants.cs` | 10 | 0 |
| `src/backend/Source/Features/Identity/Core/Entities/AuthToken.cs` | 11 | 0 |
| `src/backend/Source/Features/Identity/Core/Entities/Configuration/AuthTokenConfiguration.cs` | 4 | 0 |
| `src/backend/Source/Features/Identity/Core/SessionGrants.cs` | 8 | 10 |
| `src/backend/Source/Features/Identity/Core/Sessions/ISessionStore.cs` | 49 | 0 |
| `src/backend/Source/Features/Identity/Core/Sessions/InMemorySessionStore.cs` | 152 | 0 |
| `src/backend/Source/Features/Identity/Core/Sessions/RedisSessionStore.cs` | 197 | 0 |
| `src/backend/Source/Features/Identity/Core/Sessions/RedisSetting.cs` | 15 | 0 |
| `src/backend/Source/Features/Identity/Core/Sessions/SessionAuthentication.cs` | 67 | 0 |
| `src/backend/Source/Features/Identity/Core/Sessions/SessionClaims.cs` | 89 | 0 |
| `src/backend/Source/Features/Identity/Core/Sessions/SessionIssuer.cs` | 60 | 0 |
| `src/backend/Source/Features/Identity/Core/Sessions/SessionPrincipalValidator.cs` | 69 | 0 |
| `src/backend/Source/Features/Identity/Core/Sessions/SessionRecord.cs` | 45 | 0 |
| `src/backend/Source/Features/Identity/Core/Sessions/SessionStoreRegistration.cs` | 48 | 0 |
| `src/backend/Source/Features/Identity/Core/TenantAuthorizationService.cs` | 20 | 7 |
| `src/backend/Source/Features/Identity/Endpoints/Account/GetInfoEndpoint.cs` | 58 | 68 |
| `src/backend/Source/Features/Identity/Endpoints/Account/SignoutEndpoint.cs` | 13 | 3 |
| `src/backend/Source/Features/Identity/Endpoints/Account/TokenEndpoint.cs` | 9 | 6 |
| `src/backend/Source/Features/Identity/Endpoints/Account/TokenService.cs` | 56 | 9 |
| `src/backend/Source/Features/Identity/IdentityFeature.cs` | 8 | 0 |
| `src/backend/Source/Features/Localization/Core/Resources/ar.json` | 2 | 1 |
| `src/backend/Source/Features/Localization/Core/Resources/en.json` | 2 | 1 |
| `src/backend/Source/Features/Localization/Core/Resources/es.json` | 2 | 1 |
| `src/backend/Source/Features/Localization/Core/Resources/fr.json` | 2 | 1 |
| `src/backend/Source/Features/Localization/Core/Resources/hi.json` | 2 | 1 |
| `src/backend/Source/Features/Localization/Core/Resources/ru.json` | 2 | 1 |
| `src/backend/Source/Features/Localization/Core/Resources/ur.json` | 2 | 1 |
| `src/backend/Source/Features/Localization/Core/Resources/zh.json` | 2 | 1 |
| `src/backend/Source/Features/Tenancy/Core/FeatureManagement/PermissionFeatureFilter.cs` | 5 | 5 |
| `src/backend/Source/Helper.cs` | 1 | 44 |
| `src/backend/Source/Middleware/SessionStoreUnavailableMiddleware.cs` | 79 | 0 |
| `src/backend/Source/Migrations/20260928232338_AddAuthTokenSessionId.Designer.cs` | 895 | 0 |
| `src/backend/Source/Migrations/20260928232338_AddAuthTokenSessionId.cs` | 53 | 0 |
| `src/backend/Source/Migrations/AppDbContextModelSnapshot.cs` | 5 | 1 |
| `src/backend/Source/Processors/ExceptionProcessor.cs` | 14 | 0 |
| `src/backend/Source/Program.cs` | 13 | 0 |
| `src/backend/Source/appsettings.json` | 5 | 1 |
| `src/backend/Tests/AppTestsBase.cs` | 18 | 0 |
| `src/backend/Tests/Fakes/FaultInjectingSessionStore.cs` | 69 | 0 |
| `src/backend/Tests/Fakes/TestDoubles.cs` | 20 | 0 |
| `src/backend/Tests/Features/Identity/Core/AuthTokenServiceTests.cs` | 4 | 24 |
| `src/backend/Tests/Features/Identity/Endpoints/Account/SignupTests.cs` | 1 | 25 |
| `src/backend/Tests/Features/Identity/Sessions/InMemorySessionStoreTests.cs` | 184 | 0 |
| `src/backend/Tests/Features/Identity/Sessions/SessionClaimsTests.cs` | 54 | 0 |
| `src/backend/Tests/Features/Identity/Sessions/SessionStoreUnavailableTests.cs` | 133 | 0 |
| `src/backend/Tests/Features/Identity/Sessions/SessionTokenTests.cs` | 358 | 0 |
| `src/backend/Tests/TestsHelper.cs` | 18 | 0 |

## The brief this was built from

The task file is reproduced verbatim below. It is the most complete statement of intent
this change has: what to build, what to leave alone, and how anyone could tell it worked.

---

Tokens should carry only who the caller is (`NameIdentifier`) and which session (`sid`). Everything else about a session lives in a session record in the store, and every authenticated request reads it. This task builds the store, the slim tokens and the check on each request. These three have to ship together. Revoking sessions when access changes comes in tasks 02–04. After this task, a session is still stale until it is renewed or signed out, but it now lives in the store, where later tasks can delete it.

## Scope
- A session record in the Identity slice: session id, user id, username, email, `IsPlatform`, acting `TenantId?`, role names, permission names, created-at, expires-at. Its TTL is `Auth:RefreshTokenValidity`.
- `ISessionStore` in the Identity slice, with these operations: create, get, delete-one, revoke-by-user, revoke-by-user-in-tenant and revoke-by-tenant.
  - Keep secondary indexes: a set of session ids per user and one per tenant.
  - Index entries left behind by expired sessions are tolerated and pruned when read.
- A Redis implementation that uses StackExchange.Redis directly, not `IDistributedCache`. Every key is prefixed with `Redis:InstanceName`.
- An in-memory implementation, registered as a singleton **only** under `Testing`, so the whole test run shares one store. The backend tests and the gate must still need only PostgreSQL.
- Configuration in `appsettings.json`: `ConnectionStrings:Redis` (`localhost:6379`) and `Redis:InstanceName`. Outside `Testing`, a missing Redis connection string fails startup, the same way the JWT key placeholder guard does.
- Record the session id on each stored refresh token (an `AuthToken` column, plus its migration), so that each refresh row can be matched to its session. Task 02 needs this to revoke "all of a user's sessions but the current one".
- Minting:
  - Sign-in (`TokenEndpoint`), refresh (`TokenService`) and switch/exit (`TenantAuthorizationService.ReissueSessionAsync`) compute the session with `SessionGrants` exactly as today and write it to the store.
  - Each then mints a JWT and a cookie that hold only `NameIdentifier` and `sid`.
  - `Helper.CreateClaims` no longer puts roles, permissions, `tenant_id`, `is_platform`, name or email into a token.
- Refresh keeps every check that `SetRenewalPrivilegesAsync` makes today. It deletes the session it replaces and creates a new one. Switch and exit do the same.
- Sign-out (`SignoutEndpoint`) deletes the current session as well as the refresh tokens.
- A validation step on every authenticated request. It runs after the JWT bearer or cookie handler authenticates the caller, and before authorization.
  - It loads the session named by `sid`.
  - If the session is missing or expired, or its user id does not match `NameIdentifier`, authentication fails and the caller gets 401.
  - If the session is found, it is projected onto the **in-memory** principal as the claims the code reads today: `ClaimTypes.Role`, `ClaimConstants.Permission`, `ClaimConstants.TenantId`, `ClaimConstants.IsPlatform`, name and email.
  - This projection is the only place a session is turned into claims.
  - These must keep working unchanged: `Permissions(...)`, `ICurrentUserService`, `TenantContextProcessor`, `ErrorLocalization`, `AuthorizationRefusalResultHandler`, `HangfireAuthorizationFilter` and the rate limiter.
- If the store cannot be reached, the request fails closed with 503 and the code `sessionStoreUnavailable`. It is never answered with 401, and the caller is never treated as anonymous. Add `ErrorCodes.SessionStoreUnavailable` and an `error.server.sessionStoreUnavailable` entry in **every** resource file.
- `GetInfoEndpoint` returns the session's roles and permissions instead of recomputing them from the database. `Tenants` is still read from the database.
- `scripts/redis-ready.mjs`: a TCP probe on the host and port of `ConnectionStrings:Redis`, modelled on `pg-ready.mjs`. Add it to `verify.service.dependsOn` in `agentic.config.json`. Do **not** add it to `cycle.preflight`.
- Tests:
  - Rewrite `SignupTests.PermissionClaimsOf` and `AuthTokenServiceTests.TenantClaimOf` to assert against the session instead of the token.
  - A decoded access token and the cookie ticket hold only `NameIdentifier` and `sid`.
  - A token whose `sid` is not in the store answers 401.
  - An unreachable store answers 503 `sessionStoreUnavailable` and never falls through to anonymous. Use a test-only failing store, swapped in for that case only.
  - The access token that sign-out signed out answers 401 immediately.
  - Refresh and switch/exit delete the session they replace.

## Out of scope — do not touch
- Revoking sessions from administrative endpoints. Tasks 02, 03 and 04 own that. Do not edit:
  - `UserUpdateEndpoint`, `UserDeleteEndpoint`, `ResetPasswordEndpoint` or `ChangePasswordEndpoint`;
  - the role endpoints;
  - any Tenancy endpoint.
- Where refresh tokens are stored (they stay in `identity.AuthTokens`), and the refresh route, request and response shapes.
- `CreateProjectGenerator` and the `new-project` skill (task 05).
- `CLAUDE.md`, `tool/EasyForNetTool/new-project-claude.md`, the skills, and the `SlidingExpiration` comment in `Program.cs` (task 06).
- Caching anything else, Hangfire storage, and any active-sessions screen or endpoint.
- Sign-in rules: `tenantRequired`, email verification, and which tenant a session enters.
- The web app. It needs no change, because a 401 already drives `baseQueryWithReauth`.
- A docker-compose file or any other new root-level file.

## Done when
- `npm run verify` passes, and `npm run gate` still needs only PostgreSQL.
- A decoded access token and the cookie ticket contain `NameIdentifier` and `sid` and nothing else about the session.
- Every existing test still passes, including the ones that currently call `RenewAsync()`. Renewal still re-examines the account and the tenant: a deactivated account is refused, and a tenant that has been suspended, deleted or left is dropped.
- An unknown `sid` answers 401.
- An unreachable store answers 503 `sessionStoreUnavailable`, with a localized message in every culture.
- The access token that sign-out signed out answers 401 immediately.
- `GetInfoEndpoint` returns exactly the roles and permissions the API enforces for that session.
- By hand, under Development with Redis running: sign in, browse the admin pages, switch tenant and sign out in the web app. Everything works as before.
