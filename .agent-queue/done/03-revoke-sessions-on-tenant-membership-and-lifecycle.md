Revoke live sessions when a tenant member is removed or re-roled, or the tenant is suspended or deleted
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
