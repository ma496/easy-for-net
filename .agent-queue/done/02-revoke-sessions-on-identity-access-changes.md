Revoke live sessions when a user, their password or a role's grants change
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
