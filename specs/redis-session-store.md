Keep sign-in sessions in Redis and validate them on every request, so access changes revoke live sessions at once

Today a session's authority is fixed when its token is minted: `Helper.CreateClaims` writes the user,
roles, every permission, `tenant_id` and `is_platform` into the JWT and the auth cookie, and nothing
re-reads them until the next refresh. So an administrator who deactivates a user, removes them from a
tenant or suspends the tenant does not stop them — the token keeps working until
`Auth:AccessTokenValidity` runs out.

After this change the token and the cookie carry only **who** (`NameIdentifier`) and **which session**
(`sid`). Everything else a session is — username, email, `IsPlatform`, the acting tenant, roles, the
permissions `SessionGrants` computes — lives in a session record in Redis, read on every authenticated
request. Any change to what a user may do **deletes the affected sessions and their refresh tokens**,
so the next request answers 401, the refresh fails, and the web app signs the user out.

## Scope

### Session store

- A session record: session id, user id, username, email, `IsPlatform`, acting `TenantId?`, role
  names, permission names, created-at and expires-at. Its TTL is `Auth:RefreshTokenValidity` — the
  longest a session can live without being re-issued.
- `ISessionStore` in the Identity slice, with create, get, delete-one, and revoke-by-user,
  revoke-by-user-in-tenant and revoke-by-tenant. The revoke-by operations need secondary indexes (a
  set of session ids per user and per tenant), so the Redis implementation uses StackExchange.Redis
  directly rather than `IDistributedCache`. Index entries left behind by an expired session are
  tolerated and pruned when they are read.
- Two implementations: Redis for every environment except `Testing`, and an in-memory implementation
  used only under `Testing`, registered as a singleton so the whole test run shares one store. The
  gate and the backend tests must keep needing PostgreSQL only.
- Configuration: `ConnectionStrings:Redis` (`localhost:6379` in `appsettings.json`) and
  `Redis:InstanceName`, which prefixes every key so several applications can share one server.
  Outside `Testing`, a missing Redis connection string fails startup, like the JWT key placeholder
  guard. `CreateProjectGenerator` writes `Redis:InstanceName` as `<Name>:` for a generated project
  (`<Name>Test:` for Testing), next to where it already rewrites the connection strings.

### Tokens carry only the user and the session

- Sign-in (`TokenEndpoint`), refresh (`TokenService`) and tenant switch/exit
  (`TenantAuthorizationService.ReissueSessionAsync`) compute the session with `SessionGrants` exactly
  as today, write it to the store, and mint a JWT and cookie containing only `NameIdentifier` and
  `sid`. `Helper.CreateClaims` no longer emits roles, permissions, `tenant_id`, `is_platform`, name or
  email into a token.
- Refresh keeps every check `SetRenewalPrivilegesAsync` makes today, deletes the session it replaces,
  and creates a new one. Switch/exit do the same with the session they replace.
- Sign-out deletes the current session as well as the refresh tokens, so an access token already
  issued stops working immediately.

### Validation on every request

- After the JWT bearer or cookie handler authenticates a caller, and before authorization runs, the
  session named by `sid` is loaded. If it is missing or expired, or its user id does not match
  `NameIdentifier`, authentication fails and the caller gets 401. That is the same 401 the web app's
  `baseQueryWithReauth` already turns into a refresh, and then a sign-out when the refresh fails.
- A session that exists is projected onto the **in-memory** principal as the claims the code reads
  today: `ClaimTypes.Role`, `ClaimConstants.Permission`, `ClaimConstants.TenantId`,
  `ClaimConstants.IsPlatform`, name and email. So FastEndpoints `Permissions(...)`,
  `ICurrentUserService`, `TenantContextProcessor`, `ErrorLocalization`,
  `AuthorizationRefusalResultHandler`, `HangfireAuthorizationFilter` and the rate limiter keep working
  without changes. The projection is the only thing that turns a session into claims; nothing reads
  the store for authorization anywhere else.
- If the store cannot be reached, the request fails closed with a coded error that is not 401 (503,
  `sessionStoreUnavailable`, shipped in every resource file). It must never be treated as anonymous,
  and it must not be answered with 401: a 401 would make the web app refresh in a loop.
- `GetInfoEndpoint` returns the session's roles and permissions instead of recomputing them from the
  database, so the web app gates on exactly what the API enforces. `Tenants` (the account's
  memberships) is still read from the database.

### Revocation — every access change revokes

Each of these operations deletes the affected sessions **and** their `AuthToken` refresh rows. It
runs after the database change commits, so a rolled-back change revokes nothing. Revocation goes
through `ISessionStore` in the Identity slice. The Tenancy slice reaches it through an
`[AllowOutside]` interface, following the feature-isolation rules.

| Change | Sessions revoked |
|---|---|
| User deactivated (`UserUpdateEndpoint`, `IsActive` → false) or deleted (`UserDeleteEndpoint`) | every session of that user |
| User's roles changed (`UserUpdateEndpoint`) | that user's sessions in the tenant the roles belong to (platform scope for platform roles) |
| Password reset (`ResetPasswordEndpoint`) | every session of that user |
| Own password changed (`ChangePasswordEndpoint`) | every session of that user except the current one |
| Member removed (`TenantMemberRemoveEndpoint`) or member's roles replaced (`TenantMemberUpdateRolesEndpoint`) | that user's sessions in that tenant |
| Role permissions changed (`ChangePermissionsEndpoint`), role updated in a way that changes what it grants, or role deleted (`RoleDeleteEndpoint`) | the sessions of every user holding the role, in the role's scope |
| Tenant suspended (`TenantSuspendEndpoint`) or deleted (`TenantDeleteEndpoint`) | every session in that tenant |
| Tenant's edition changed (`TenantUpdateEndpoint`), a tenant's feature values changed (`FeatureValueUpdateEndpoint`) | every session in that tenant |
| Edition's feature values changed or edition deleted (`EditionUpdateEndpoint`, `EditionDeleteEndpoint`) | every session in every tenant on that edition |

`DataSeeder`'s startup reconciliation does not revoke anything. A permission deleted from code simply
disappears from sessions minted after the deploy.

### Local run and the verify loop

- `scripts/redis-ready.mjs`, a TCP probe on the host and port of `ConnectionStrings:Redis`, modelled
  on `pg-ready.mjs`, added to `verify.service.dependsOn` in `agentic.config.json`: `serve-api` runs
  under Development, which needs Redis. It is **not** added to `cycle.preflight`, because the tests do
  not need Redis.
- Update the `new-project` skill's post-create steps to start a local Redis (for example
  `docker run -d -p 6379:6379 redis:7`).

### Documentation

Rewrite every statement that authority is "decided once, when a token is minted" and changes only "at
the next token renewal". That covers the **Auth**, **Permissions** and **Features (entitlements)**
paragraphs of `CLAUDE.md`, and the same text in `tool/EasyForNetTool/new-project-claude.md`. Enforcement
of plan gating becomes "computed when the session is created, and the session is revoked when the plan
changes". Update the `multi-tenancy`, `permissions` and `feature-management` skills wherever they
repeat the mint-time rule. Update the comment on `SlidingExpiration = false` in `Program.cs`.

## Out of scope — do not touch

- Where refresh tokens are stored: they stay in PostgreSQL (`identity.AuthTokens`), and the refresh
  endpoint's route, request and response shapes do not change.
- Caching anything else (feature values, localization, permission definitions). Hangfire storage stays
  on PostgreSQL.
- Any "active sessions" screen or endpoint for listing or signing out devices.
- Sign-in rules (which tenant a session enters, `tenantRequired`, email verification) and the web
  app's sign-in, switch and refresh flows, beyond what reading roles from the session requires.
- A docker-compose file or any other new root-level file.

## Done when

- `npm run verify` passes, and the gate still needs only PostgreSQL.
- A decoded access token and the cookie ticket contain `NameIdentifier` and `sid` and no role,
  permission, `tenant_id` or `is_platform` claim. Update `SignupTests.PermissionClaimsOf` and
  `AuthTokenServiceTests.TenantClaimOf` to assert this against the session instead.
- For each row of the revocation table there is an integration test: a session is signed in, the
  change is made by an administrator, and the **old access token without renewing** gets 401 on its
  next request. The refresh token is then refused too. Sessions the row does not name keep working
  (for example, removing a user from tenant A leaves their tenant B session alive, and changing one
  role leaves users of other roles alone).
- Sign-out makes the access token it signed out answer 401 immediately.
- A token whose `sid` is not in the store gets 401. The store being unreachable gets the 503 coded
  error, not 401 and not anonymous access.
- The tests that pinned the old "stale until renewed" behaviour are rewritten for the new rule, not
  deleted: `SessionFeatureGatingTests.A_Session_Already_Issued_Keeps_What_It_Was_Issued_With` and
  `ChangePasswordTests.ChangePassword_Replaces_The_Credentials_And_Keeps_The_Session` (which now
  asserts that the current session survives and other sessions of the user do not).
  `TenantSuspensionTests`, `TenantSwitchTests` and `TenantMemberUpdateRolesTests` must not need
  `RenewAsync()` to see a change any more.
- By hand, with Redis and the web app running: sign in as a tenant user in one browser. As
  `tenantadmin` in another, remove that user from the tenant. The first browser's next navigation
  lands on `/signin`.
