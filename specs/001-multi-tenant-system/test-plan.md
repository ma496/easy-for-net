# Test plan - Multi-tenant system

Spec: `specs/001-multi-tenant-system/spec.md` (AC-001..AC-149).
Plan: `specs/001-multi-tenant-system/plan.md`.
Governing skills: `.claude/skills/backend-tests`, `.claude/skills/frontend-tests`,
`.claude/skills/coding-conventions`.

## How to read this

Every acceptance criterion in `spec.md` appears exactly once in the coverage matrix below, in spec
order, with the test that discharges it. A row gives the test file, the test method, the fixture or
seeder it uses, and **the assertion that would fail if the behaviour were removed** — the last column
is the point of the row; a row whose assertion would still pass with the behaviour deleted is not
coverage.

Two projects hold the tests, and the file path says which:

| Path prefix | Project | Runner |
| --- | --- | --- |
| `src/backend/Tests/…` | `src/backend/Tests/Backend.Tests.csproj` | `dotnet test src/backend/Tests/Backend.Tests.csproj` (xUnit v3 + FastEndpoints.Testing + FluentAssertions + Bogus, real PostgreSQL from `appsettings.Testing.json`) |
| `src/frontend/web/…` | `src/frontend/web` | `npm run test` (`vitest run`) from `src/frontend/web` |

Backend method names are `Pascal_Snake`; one test class per endpoint named `<Entity><Action>Tests`,
mirroring the source tree, as `backend-tests` requires. Frontend tests are colocated `*.test.ts`
files covering **pure logic only** — no DOM, no rendering, no network — as `frontend-tests` requires.

Criteria phrased as "The system shall include a test proving …" (AC-086..AC-092, AC-128..AC-132,
AC-146..AC-149) are met by the existence of the named file and method; they are listed in the matrix
like every other criterion so `/verify` can trace them.

## Test projects and how they run

- The backend suite boots the real host with environment `Testing`; `SharedContextFixture` migrates a
  fresh database, signs in, seeds through `TestsDataSeeder`, and drops the database at teardown. That
  fresh migrate is itself the standing evidence for AC-085.
- A startup failure is almost always PostgreSQL; see the `backend-tests` skill.
- The frontend suite has no `vitest.config.ts` and must keep it that way: nothing in this plan needs
  `jsdom`. Where a criterion is only observable through rendering it is listed under
  **Criteria not covered automatically** rather than smuggled in behind a new DOM environment.

## Fixtures, seeders and helpers

### Seeder invariants (new and changed)

`src/backend/Tests/Seeder/TestTenants.cs` (**new**, mirroring `TestRoles`/`TestUsers`):

| Member | Meaning |
| --- | --- |
| `BootstrapTenantId` | the system-created bootstrap tenant the migration and `DataSeeder` produce (`TenancyConstants.BootstrapTenantId`) |
| `SecondTenantId` | a second seeded tenant, so "another tenant" exists for read-only assertions without every test provisioning two |
| `SetTenantIds(…)` | called once from `TestsDataSeeder.SeedAsync` |

`src/backend/Tests/Seeder/TestRoles.cs` (**changed**) additionally captures:

| Member | Meaning | Why |
| --- | --- | --- |
| `LimitedTenantRoleId` | a role **inside the bootstrap tenant** holding exactly one permission (`Allow.Tenant_View`) | every existing seeded role holds every permission, which is precisely why AC-088 exists |
| `SecondTenantAdminRoleId` | the system-created administrator role of `SecondTenantId` | lets a test assert "administrator here, nothing there" without provisioning |
| `PlatformAdminRoleId` | the platform `Admin` role (`TenantId == null`) | AC-083, AC-121 |

`src/backend/Tests/Seeder/TestUsers.cs` (**changed**) additionally captures `LimitedUserId`
(`limited` — member of the bootstrap tenant holding `LimitedTenantRole` only), `NoMembershipUserId`
(`nomember` — an active account with **no** membership) and `DualTenantUserId` (`dual` — an active
membership in **both** seeded tenants: administrator in `SecondTenant`, `LimitedTenantRole` in the
bootstrap tenant).

**Load-bearing seeder invariant.** Every seeded account *except* `dual` holds exactly **one** active
membership. AC-123 then auto-selects that tenant at sign-in, so `SetAuthTokenAsync()` keeps working
unchanged for the existing test methods across `Users`, `Roles`, `Account` and `Notifications`.
`dual` is the only account that exercises the "no active tenant at sign-in" path
(AC-140/AC-149). Giving `admin`, `test`, `testone` or `testtwo` a second membership would break the
whole existing suite; do not.

`src/backend/Tests/Seeder/TestsDataSeeder.cs` (**changed**) additionally: places the existing seeded
users in the bootstrap tenant, creates `SecondTenant` with its administrator role and a `secondadmin`
account, creates `LimitedTenantRole`, `limited`, `nomember` and `dual`, and writes a `StoredFile` row
for every seeded profile image so that authenticated file reads (AC-098) do not break seeded data.

### `TenancyTestsBase` (new)

`src/backend/Tests/Features/Tenancy/TenancyTestsBase.cs`, an abstract `AppTestsBase` in the shape of
the existing `NotificationsTestsBase`. Every tenancy test that asserts on rows uses these factories
instead of seeded ids, which is what discharges AC-090:

| Helper | Produces |
| --- | --- |
| `CreateTenantAsync(status = Active)` | a tenant whose identifier is a unique `t-…` value valid under AC-101, created under a platform scope |
| `CreateTenantRoleAsync(tenantId, params string[] permissions)` | a role inside that tenant holding exactly those permissions |
| `CreateTenantUserAsync(tenantId, params Guid[] roleIds)` | an account with `TestUsers.DefaultPassword`, an active membership and those roles |
| `CreateAccountWithoutMembershipAsync()` | an active account with no membership (AC-050, AC-122, AC-136 paths) |
| `SignInAsAsync(username, tenantId)` | token, then `TenantSwitchEndpoint`, then sets the Bearer header |
| `ClientForAsync(username, tenantId)` | a **separate** `HttpClient` with its own Bearer header, for tests needing two identities at once (AC-081) |
| `TenantScopedAsync(tenantId, Func<Task>)` | runs a block under `ITenantContext.BeginTenant(tenantId)` for direct `DbContext` arrangement |

### Frontend fixtures

Small local factories declared in the test file, per the `frontend-tests` skill — no shared fixture
module:

- `userInfo({ tenants, activeTenant, permissions })` building a `GetUserInfoResponse`.
- `stateWithPermissions(...)` already exists in
  `src/frontend/web/lib/utils/authentication-and-authorization.test.ts`; extend it with a tenant
  argument rather than writing a second factory beside it.

## Testability requirements this plan imposes on the implementation

Three decisions are made here because the criterion is otherwise observable only through a rendered
component, which this repository's frontend suite deliberately does not cover:

1. **`src/frontend/web/lib/utils/tenant-routing.ts`** (new, re-exported from `lib/utils/index.ts`) —
   pure `resolveTenantLanding(user)` returning `'/select-tenant' | '/no-tenant' | null`, and
   `isActiveTenantStale(user)`. The landing decision behind AC-126/AC-127/AC-142 lives here rather
   than inline in `App.tsx`.
2. **`src/frontend/web/store/tenant-cache.ts`** (new) — `tenantChangedActions(userInfo)` and
   `signedOutActions()` returning the ordered action arrays the switcher and sign-out dispatch, with
   `appApi.util.resetApiState()` first. AC-028/AC-125/AC-132 then assert on an array instead of an
   untestable side effect inside a component.
3. **`src/backend/Source/HangfireAuthorizationFilter.cs`** exposes
   `internal static bool IsAuthorized(ClaimsPrincipal user)` that `Authorize(DashboardContext)`
   delegates to, so AC-047 is a unit test over a principal rather than a fabricated
   `DashboardContext`.

These need to agree with `frontend-contract.md`; see **Open questions**.

## Backend test file inventory

New unless marked *changed*. Paths are repository-relative.

| File | Purpose | Criteria |
| --- | --- | --- |
| `src/backend/Tests/Seeder/TestTenants.cs` | seeded tenant ids | fixture |
| `src/backend/Tests/Seeder/TestRoles.cs` *changed* | `LimitedTenantRoleId`, `SecondTenantAdminRoleId`, `PlatformAdminRoleId` | AC-088 |
| `src/backend/Tests/Seeder/TestUsers.cs` *changed* | `LimitedUserId`, `NoMembershipUserId`, `DualTenantUserId` | AC-050, AC-140 |
| `src/backend/Tests/Seeder/TestsDataSeeder.cs` *changed* | tenant-aware seed, `StoredFile` rows for seeded images | fixture |
| `src/backend/Tests/AppTestsBase.cs` *changed* | `SetAuthTokenAsync` gains an optional tenant; `SwitchTenantAsync` | fixture |
| `src/backend/Tests/TestsHelper.cs` *changed* | token helper that also selects a tenant | fixture |
| `src/backend/Tests/Features/Tenancy/TenancyTestsBase.cs` | the factories above | AC-090 |
| `src/backend/Tests/Architect/TenantScopingTests.cs` | model walk: scoped, or exempt with a written reason | AC-091 |
| `src/backend/Tests/Features/Tenancy/Core/TenantFilterTests.cs` | the isolation kernel | AC-030..AC-034, AC-036, AC-037, AC-080 |
| `src/backend/Tests/Features/Tenancy/Core/BackgroundTenantScopeTests.cs` | work outside a request | AC-035, AC-131 |
| `src/backend/Tests/Features/Tenancy/Core/TenantIsolationTests.cs` | cross-tenant through endpoints | AC-032, AC-087, AC-111 |
| `src/backend/Tests/Features/Tenancy/Core/TenantUniquenessTests.cs` | reserved names, database constraints | AC-102, AC-144, AC-147 |
| `src/backend/Tests/Features/Tenancy/Core/TenantConcurrencyTests.cs` | assignment replacement | AC-081 |
| `src/backend/Tests/Features/Tenancy/Core/TenantSchemaTests.cs` | entity shape, indexes, pending migrations | AC-001, AC-085 |
| `src/backend/Tests/Features/Tenancy/Core/TenantSeedingTests.cs` | bootstrap and reconciliation | AC-082..AC-084, AC-121, AC-145 |
| `src/backend/Tests/Features/Tenancy/Core/MembershipActivationTests.cs` | what "active membership" is | AC-103 |
| `src/backend/Tests/Features/Tenancy/Core/TenantContextResolutionTests.cs` | established vs not established | AC-022, AC-023, AC-050 |
| `src/backend/Tests/Features/Tenancy/Core/PlatformSurfaceTests.cs` | platform-only surfaces | AC-046 |
| `src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantListTests.cs` | list, page, sort, search, filter | AC-046, AC-061..AC-066 |
| `src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantGetTests.cs` | read one | AC-010, AC-021 |
| `src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantCreateTests.cs` | create | AC-002..AC-004, AC-012, AC-042, AC-078, AC-100..AC-102 |
| `src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantUpdateTests.cs` | rename | AC-003..AC-005, AC-010, AC-011 |
| `src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantSuspendTests.cs` | suspend | AC-006, AC-011 |
| `src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantReactivateTests.cs` | reactivate | AC-008 |
| `src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantDeleteTests.cs` | delete | AC-009, AC-011, AC-079 |
| `src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantSuspensionTests.cs` | behaviour while suspended | AC-007, AC-029, AC-060, AC-070, AC-089 |
| `src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantMemberListTests.cs` | member list | AC-021, AC-061..AC-064 |
| `src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantMemberAddTests.cs` | add a member | AC-013..AC-016, AC-021, AC-078, AC-106, AC-107 |
| `src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantMemberUpdateRolesTests.cs` | replace assignments | AC-017, AC-019 |
| `src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantMemberRemoveTests.cs` | remove a member | AC-018..AC-020, AC-079 |
| `src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantOnboardTests.cs` | self-service onboarding | AC-133..AC-137, AC-146 |
| `src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantSwitchTests.cs` | selection and switching | AC-025..AC-027, AC-108, AC-109, AC-123, AC-138..AC-140, AC-148, AC-149 |
| `src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantPermissionTests.cs` | permission gating per endpoint | AC-044, AC-045, AC-088 |
| `src/backend/Tests/Middleware/SessionValidationMiddlewareTenantTests.cs` | authorization freshness | AC-020, AC-108, AC-116, AC-117, AC-127 |
| `src/backend/Tests/ErrorHandling/TenantErrorCodesTests.cs` | the error-code catalogue | AC-067 |
| `src/backend/Tests/HangfireAuthorizationFilterTests.cs` | dashboard gate | AC-047 |
| `src/backend/Tests/Features/Identity/Endpoints/Account/TokenTests.cs` | sign-in | AC-048, AC-049, AC-104, AC-105 |
| `src/backend/Tests/Features/Identity/Endpoints/Account/GetInfoTests.cs` | caller's tenants and active tenant | AC-024, AC-124 |
| `src/backend/Tests/Features/Identity/Endpoints/Account/SignupTests.cs` | self-service sign-up | AC-118..AC-120, AC-122 |
| `src/backend/Tests/Features/Identity/Endpoints/Account/AccountSelfServiceTests.cs` | self-service without a tenant | AC-051, AC-097 |
| `src/backend/Tests/Features/Identity/Endpoints/Account/SignoutTests.cs` | sign-out drops the selection | AC-125 |
| `src/backend/Tests/Features/Identity/Endpoints/Account/ChangePasswordTests.cs` *changed* | still usable with no tenant | AC-051 |
| `src/backend/Tests/Features/Identity/Core/AuthTokenServiceTests.cs` *changed* | refresh carries the tenant | AC-109 |
| `src/backend/Tests/Features/Identity/Endpoints/Users/UserListTests.cs` *changed* | tenant-scoped list | AC-093, AC-095 |
| `src/backend/Tests/Features/Identity/Endpoints/Users/UserGetTests.cs` *changed* | cross-tenant read is a miss | AC-094 |
| `src/backend/Tests/Features/Identity/Endpoints/Users/UserCreateTests.cs` *changed* | membership on create, global uniqueness | AC-048, AC-096 |
| `src/backend/Tests/Features/Identity/Endpoints/Users/UserUpdateTests.cs` *changed* | cross-tenant update is a miss | AC-094, AC-105 |
| `src/backend/Tests/Features/Identity/Endpoints/Users/UserDeleteTests.cs` *changed* | cross-tenant delete is a miss | AC-094 |
| `src/backend/Tests/Features/Identity/Endpoints/Roles/RoleListTests.cs` *changed* | tenant-scoped role list | AC-110, AC-113 |
| `src/backend/Tests/Features/Identity/Endpoints/Roles/RoleGetTests.cs` *changed* | cross-tenant role read is a miss | AC-111 |
| `src/backend/Tests/Features/Identity/Endpoints/Roles/RoleCreateTests.cs` *changed* | per-tenant name, attribution | AC-038, AC-039, AC-078, AC-112 |
| `src/backend/Tests/Features/Identity/Endpoints/Roles/RoleUpdateTests.cs` *changed* | cross-tenant rename is a miss | AC-111 |
| `src/backend/Tests/Features/Identity/Endpoints/Roles/RoleDeleteTests.cs` *changed* | protected system role, soft delete | AC-043, AC-111 |
| `src/backend/Tests/Features/Identity/Endpoints/Roles/ChangePermissionsTests.cs` *changed* | platform permission refused | AC-041, AC-043, AC-111 |
| `src/backend/Tests/Features/Identity/Endpoints/Permissions/GetDefinePermissionsTests.cs` | catalogue by tier | AC-040, AC-114, AC-115 |
| `src/backend/Tests/Features/Notifications/Endpoints/Notifications/NotificationTenancyTests.cs` | per-tenant visibility | AC-052, AC-054, AC-129 |
| `src/backend/Tests/Features/Notifications/Endpoints/Notifications/NotificationListTests.cs` *changed* | list scoped | AC-052 |
| `src/backend/Tests/Features/Notifications/Endpoints/Notifications/NotificationGetUnreadCountTests.cs` *changed* | count scoped | AC-056 |
| `src/backend/Tests/Features/Notifications/Endpoints/Notifications/NotificationMarkAllAsReadTests.cs` *changed* | the raw-SQL site | AC-033, AC-055, AC-130 |
| `src/backend/Tests/Features/Notifications/Core/NotificationServiceTests.cs` | addressing modes | AC-053 |
| `src/backend/Tests/Features/FileManagement/Endpoints/Files/FileUploadTests.cs` | upload | AC-057, AC-098, AC-099 |
| `src/backend/Tests/Features/FileManagement/Endpoints/Files/FileGetTests.cs` | download | AC-058..AC-060, AC-098 |
| `src/backend/Tests/Features/FileManagement/Endpoints/Files/FileDeleteTests.cs` | delete | AC-058, AC-098 |
| `src/backend/Tests/Features/FileManagement/Core/FileTenancyTests.cs` | the combined file proof | AC-097, AC-128 |

`src/backend/Tests/Features/FileManagement/Core/LocalStorageProviderTests.cs` and everything under
`src/backend/Tests/Architect/Features/` are **not** touched: the storage provider is unchanged by
decision D17, and the `FeatureA`/`FeatureB` types are deliberate violations used as fixtures.

## Frontend test file inventory

| File | Purpose | Criteria |
| --- | --- | --- |
| `src/frontend/web/lib/utils/authentication-and-authorization.test.ts` *changed* | `isAllowed` for a user whose permissions differ between two tenants | AC-027 (client mirror), AC-092 |
| `src/frontend/web/lib/utils/tenant-routing.test.ts` | landing decision and stale selection | AC-126, AC-127, AC-140 (client), AC-142, AC-143 |
| `src/frontend/web/store/tenant-cache.test.ts` | cache discard on switch and on sign-out | AC-028, AC-125, AC-132 |
| `src/frontend/web/store/slices/authSlice.test.ts` | active tenant and tenant list in auth state | AC-024 (client), AC-073 (state half), AC-123 (client) |
| `src/frontend/web/lib/utils/api-error-helpers.test.ts` | tenant error codes become translated messages | AC-068 (client), AC-069, AC-070 |
| `src/frontend/web/auth-urls.test.ts` | route gating for every tenant and platform screen | AC-071, AC-072, AC-074, AC-141, AC-143 |
| `src/frontend/web/allow.test.ts` | the permission mirror's exact string values | AC-044 (web half) |
| `src/frontend/web/i18n/locales.test.ts` | key parity across the eight locale files | AC-069, AC-076 |

## Coverage matrix

Column meanings: **File** and **Test method** identify the test; **Fixture** names the seeder,
factory or arrangement it stands on; **Failing assertion** is what breaks if the behaviour is
deleted. Backend paths under `src/backend/Tests/…` are abbreviated with `T/`; frontend paths under
`src/frontend/web/…` with `W/`.

### Tenant lifecycle — AC-001..AC-012

| AC | File | Test method | Fixture | Failing assertion |
| --- | --- | --- | --- | --- |
| AC-001 | `T/Features/Tenancy/Core/TenantSchemaTests.cs` | `Tenant_Entity_Shape` | `DbContext.Model` | the `Tenant` entity type exposes `Name`, `Identifier`, `IdentifierNormalized`, `Status`, `CreatedBy/At`, `UpdatedBy/At`, `IsDeleted/DeletedAt`, and a unique index on `IdentifierNormalized` named `IX_Tenants_Identifier` |
| AC-002 | `T/…/Tenants/TenantCreateTests.cs` | `Valid_Input` | `SetAuthTokenAsync()` (seeded platform admin), Bogus `Faker<TenantCreateRequest>` with a unique identifier | 200 and `res.Id != Guid.Empty`, and the persisted row read with `AcrossAllTenants()` has `Status == TenantStatus.Active` |
| AC-003 | `T/…/Tenants/TenantCreateTests.cs` | `Duplicate_Identifier_Ignoring_Case` | `CreateTenantAsync()` supplies the incumbent identifier | `ProblemDetails` carries code `tenantIdentifierAlreadyExists` on field `identifier`, **and** `DbContext.Tenants.AcrossAllTenants().Count(t => t.IdentifierNormalized == …) == 1` (nothing persisted) |
| AC-003 | `T/…/Tenants/TenantUpdateTests.cs` | `Rename_To_Existing_Identifier` | two tenants from `CreateTenantAsync()` | same code and field; the renamed tenant's stored identifier is unchanged |
| AC-003 | `T/Features/Tenancy/Core/TenantUniquenessTests.cs` | `Soft_Deleted_Identifier_Is_Still_Reserved` | tenant created then deleted through `TenantDeleteEndpoint` | re-creating with that identifier returns `tenantIdentifierAlreadyExists` rather than 200 |
| AC-004 | `T/…/Tenants/TenantCreateTests.cs` | `Invalid_Input` | Bogus request with an empty name and a 2-character identifier | `res.Errors.Select(e => e.Name)` equals `["name", "identifier"]` and status is 400 |
| AC-004 | `T/…/Tenants/TenantUpdateTests.cs` | `Invalid_Input` | as above against an existing tenant | 400 with the offending field named; the stored row is unchanged |
| AC-005 | `T/…/Tenants/TenantUpdateTests.cs` | `Valid_Input` | `CreateTenantAsync()`, `SetAuthTokenAsync()` | reloaded row has the new name, `UpdatedBy == TestUsers.AdminUserId` and `UpdatedAt` not null and `>= CreatedAt` |
| AC-006 | `T/…/Tenants/TenantSuspendTests.cs` | `Suspend_Active_Tenant` | `CreateTenantAsync()` plus one tenant-scoped row created inside it | `Status == TenantStatus.Suspended` **and** the row is still present when read with `AcrossAllTenants()` (retention, not deletion) |
| AC-007 | `T/…/Tenants/TenantSuspensionTests.cs` | `Suspended_Tenant_Refuses_Tenant_Scoped_Requests` | member from `CreateTenantUserAsync()` signed in before suspension | a tenant-scoped call returns 403 with code `tenantSuspended` (not 200, not 401) |
| AC-008 | `T/…/Tenants/TenantReactivateTests.cs` | `Reactivate_Suspended_Tenant` | suspended tenant from `CreateTenantAsync(Suspended)` | `Status == TenantStatus.Active` after the call |
| AC-008 | `T/…/Tenants/TenantSuspensionTests.cs` | `Reactivation_Restores_Member_Access` | the same member token as the suspension test, never re-issued | the member's tenant-scoped call returns 200 again **without** signing in again |
| AC-009 | `T/…/Tenants/TenantDeleteTests.cs` | `Delete_Tenant` | `CreateTenantAsync()` with one scoped row | `TenantGetEndpoint` returns `tenantNotFound`, the scoped row is invisible through a normal query, and `DbContext.Tenants.IgnoreQueryFilters().Any(t => t.Id == id)` is still true |
| AC-010 | `T/…/Tenants/TenantGetTests.cs` | `Unknown_And_Deleted_Tenant_Are_Indistinguishable` | one deleted tenant plus a random `Guid` | both calls return the **same** status and the same code `tenantNotFound`, and neither body contains the tenant name |
| AC-011 | `T/…/Tenants/TenantUpdateTests.cs` | `Cannot_Update_System_Created_Tenant` | `TestTenants.BootstrapTenantId` (read-only use; the mutation is refused) | code `systemCreatedTenantCannotBeModified` and the bootstrap row's name unchanged |
| AC-011 | `T/…/Tenants/TenantSuspendTests.cs` | `Cannot_Suspend_System_Created_Tenant` | `TestTenants.BootstrapTenantId` | same code; `Status` still `Active` |
| AC-011 | `T/…/Tenants/TenantDeleteTests.cs` | `Cannot_Delete_System_Created_Tenant` | `TestTenants.BootstrapTenantId` | same code; `IsDeleted` still false |
| AC-012 | `T/…/Tenants/TenantCreateTests.cs` | `Records_Audit_Fields` | `SetAuthTokenAsync()` | created row has `CreatedBy == TestUsers.AdminUserId` and `CreatedAt` within the test window; after an update, `UpdatedBy`/`UpdatedAt` are set |

### Naming and normalization — AC-100..AC-102

| AC | File | Test method | Fixture | Failing assertion |
| --- | --- | --- | --- | --- |
| AC-100 | `T/…/Tenants/TenantCreateTests.cs` | `Name_Length_Rules` | `[Theory]` over `""`, `"a"`, 101 characters, `"  ab  "` | the first three return 400 with error name `name`; the last returns 200 and stores `Name == "ab"` (trimmed) |
| AC-101 | `T/…/Tenants/TenantCreateTests.cs` | `Identifier_Format_Rules` | `[Theory]` over `"ab"`, 51 characters, `"-acme"`, `"acme-"`, `"ac--me"`, `"ac_me"`, `"ac me"`, and the valid `"a-1"` | every invalid value returns 400 with error name `identifier`; `"a-1"` returns 200 |
| AC-102 | `T/Features/Tenancy/Core/TenantUniquenessTests.cs` | `Identifier_Is_Stored_Trimmed_With_A_Normalized_Copy` | tenant created with a padded, mixed-case identifier | stored `Identifier` equals the trimmed input, `IdentifierNormalized` equals its lower-case form, and a duplicate submitted in a different case is refused — proving the comparison runs against the normalized column |

### Self-service onboarding — AC-133..AC-137

| AC | File | Test method | Fixture | Failing assertion |
| --- | --- | --- | --- | --- |
| AC-133 | `T/…/Tenants/TenantOnboardTests.cs` | `Valid_Input` | `CreateAccountWithoutMembershipAsync()` then its own token | 200; created tenant `Status == Active`; a `TenantMembership` row exists for the creator; the creator holds the tenant's `SystemCreated` administrator role; a following `GetInfoEndpoint` call reports that tenant as active |
| AC-134 | `T/…/Tenants/TenantOnboardTests.cs` | `Applies_The_Same_Rules_As_Platform_Create` | as above, plus a tenant from `CreateTenantAsync()` to collide with | duplicate returns `tenantIdentifierAlreadyExists` on `identifier` and invalid input returns the same field names as `TenantCreateTests.Invalid_Input` |
| AC-135 | `T/…/Tenants/TenantOnboardTests.cs` | `Grants_No_Platform_Permission` | the onboarded account | its permission claims after the switch do not contain `Allow.Platform_Administration`, and `TenantSuspendEndpoint` against another tenant returns 403 |
| AC-136 | `T/…/Tenants/TenantOnboardTests.cs` | `Unauthenticated` | `ClearAuthToken()` | 401, and no tenant row with the submitted identifier exists |
| AC-137 | `T/…/Tenants/TenantOnboardTests.cs` | `Existing_Member_Can_Create_Another_Tenant` | an account that already onboarded once | second call returns 200 and the new tenant's `CreatedBy` is that account |
| AC-146 | `T/…/Tenants/TenantOnboardTests.cs` | `Creator_Is_Administrator_Only_Of_The_Tenant_Created` | two tenants: one created by the account, one from `CreateTenantAsync()` | creator holds tenant administration in its own tenant, and acting there grants nothing in the other (`TenantMemberAddEndpoint` on the other → 403 `notTenantMember`), and no other tenant's membership rows changed |

### Tenant membership — AC-013..AC-021

| AC | File | Test method | Fixture | Failing assertion |
| --- | --- | --- | --- | --- |
| AC-013 | `T/…/Tenants/TenantMemberAddTests.cs` | `Memberships_Are_Independent` | two tenants and one account from the factories | removing the account from tenant A leaves its tenant-B membership active and its tenant-B role assignments byte-identical |
| AC-014 | `T/…/Tenants/TenantMemberAddTests.cs` | `Valid_Input` | `CreateTenantAsync()`, `CreateTenantRoleAsync()` ×2, an existing account | the account's `UserRole` rows whose `Role.TenantId` is the tenant equal **exactly** the requested role set (no extra, no missing) |
| AC-015 | `T/…/Tenants/TenantMemberAddTests.cs` | `Duplicate_Membership` | a member added by the previous arrangement | code `duplicateTenantMembership` and the membership row count for that pair stays 1 |
| AC-016 | `T/…/Tenants/TenantMemberAddTests.cs` | `Unknown_User` | random `Guid` as the user id | code `userNotFound` and no membership row is created |
| AC-017 | `T/…/Tenants/TenantMemberUpdateRolesTests.cs` | `Valid_Input` | member holding roles `[A, B]` | after replacing with `[B, C]` the assignment set equals exactly `{B, C}`, and the membership row's `UpdatedBy`/`UpdatedAt` changed |
| AC-018 | `T/…/Tenants/TenantMemberRemoveTests.cs` | `Valid_Input` | member in two created tenants | membership is soft-deleted, the `User` row is still active, the other tenant's membership is untouched, and the member's next request in the removed tenant returns `tenantMembershipRevoked` |
| AC-019 | `T/…/Tenants/TenantMemberRemoveTests.cs` | `Cannot_Remove_Last_Administrator` | tenant whose only administrator is one member | code `lastTenantAdministrator` and the membership row is still active |
| AC-019 | `T/…/Tenants/TenantMemberUpdateRolesTests.cs` | `Cannot_Strip_Last_Administrator_Role` | same arrangement | same code, and the member still holds the administrator role |
| AC-020 | `T/Middleware/SessionValidationMiddlewareTenantTests.cs` | `Membership_Revocation_Takes_Effect_On_Next_Request` | member of two created tenants, token minted **before** the removal | with the unchanged token: the tenant-scoped call returns 403 `tenantMembershipRevoked` (not 401), the stored `PasswordHash` is byte-identical to before, and switching to the other tenant still returns 200 |
| AC-021 | `T/…/Tenants/TenantMemberListTests.cs` | `Non_Member_Is_Refused` | account that is a member of a different created tenant, not a platform admin | 403 with code `notTenantMember` and no member rows in the body |
| AC-021 | `T/…/Tenants/TenantMemberAddTests.cs` | `Non_Member_Is_Refused` | same | 403 `notTenantMember` and no membership row created |

### Membership semantics — AC-103..AC-107

| AC | File | Test method | Fixture | Failing assertion |
| --- | --- | --- | --- | --- |
| AC-103 | `T/Features/Tenancy/Core/MembershipActivationTests.cs` | `Active_Membership_Definition` | `[Theory]` over: no row, soft-deleted row, tenant suspended, tenant deleted, all-good | only the all-good case allows the tenant-scoped call; the other four are refused with `notTenantMember`/`tenantSuspended`/`tenantNotFound` respectively |
| AC-103 | `T/Features/Tenancy/Core/MembershipActivationTests.cs` | `Membership_Carries_No_Further_State` | `DbContext.Model` | the `TenantMembership` entity type declares no `Status`, `IsActive` or equivalent per-tenant state property |
| AC-104 | `T/Features/Identity/Endpoints/Account/TokenTests.cs` | `Deactivated_Account_Cannot_Authenticate` | account created by the factory, then `IsActive = false` | token call returns 401 with `userNotActive`, and the account's membership rows are unchanged in number and ids |
| AC-105 | `T/Features/Identity/Endpoints/Account/TokenTests.cs` | `Reactivated_Account_Keeps_Exactly_Its_Memberships` | the same account reactivated | the membership id set after reactivation equals the set captured before deactivation — nothing added, nothing removed |
| AC-106 | `T/…/Tenants/TenantMemberAddTests.cs` | `Re_Add_After_Removal` | member removed by a previous call | re-add returns 200, a **new** active membership exists, and the requested roles are exactly assigned |
| AC-107 | `T/…/Tenants/TenantMemberAddTests.cs` | `Removed_Membership_Does_Not_Block_Re_Add` | same | the response is not `duplicateTenantMembership` — the removed row is excluded from the duplicate comparison |

### Tenant context and switching — AC-022..AC-029

| AC | File | Test method | Fixture | Failing assertion |
| --- | --- | --- | --- | --- |
| AC-022 | `T/Features/Tenancy/Core/TenantContextResolutionTests.cs` | `Request_Acts_In_Exactly_One_Tenant` | `dual` signed in and switched to a created tenant | a record created through an endpoint carries `TenantId` equal to that tenant, and no row is written for any other tenant in the same request |
| AC-023 | `T/Features/Tenancy/Core/TenantContextResolutionTests.cs` | `No_Active_Tenant_Is_Refused` | `TestUsers.DualTenantUserId` signed in but not switched | tenant-scoped call returns 403 with code `noActiveTenant`, and the response body is not a list of rows from any tenant |
| AC-024 | `T/Features/Identity/Endpoints/Account/GetInfoTests.cs` | `Returns_The_Callers_Tenants` | `dual` | `res.Tenants` ids contain both seeded tenant ids and nothing the user is not a member of |
| AC-025 | `T/…/Tenants/TenantSwitchTests.cs` | `Switch_Applies_To_Subsequent_Requests` | member of two created tenants | after the switch, a list endpoint returns tenant-B rows and none of the tenant-A rows the test created; no new sign-in is performed |
| AC-026 | `T/…/Tenants/TenantSwitchTests.cs` | `Cannot_Switch_To_Non_Member_Tenant` | account that is an administrator of tenant A only | switching to tenant B returns 403 `notTenantMember` even though the caller holds every permission in tenant A |
| AC-027 | `T/…/Tenants/TenantSwitchTests.cs` | `Permissions_Come_Only_From_The_Active_Tenant` | account with `LimitedTenantRole` in A and the administrator role in B | acting in A, `RoleCreateEndpoint` returns 403; after switching to B, the same call returns 200 |
| AC-028 | `W/store/tenant-cache.test.ts` | `discards the cached tenant data when the active tenant changes` | local `userInfo(...)` factory | `tenantChangedActions(user)` contains `appApi.util.resetApiState()` and it is the **first** action in the array |
| AC-029 | `T/…/Tenants/TenantSuspensionTests.cs` | `Suspension_Keeps_The_Session_And_Offers_Another_Tenant` | member of two created tenants; one gets suspended | the tenant-scoped call returns 403 `tenantSuspended` while `GetInfoEndpoint` on the same token returns 200 and lists the other tenant — the user is neither signed out (401) nor left without an alternative |

### Authorization freshness and session-carried tenant — AC-108, AC-109, AC-138..AC-140

| AC | File | Test method | Fixture | Failing assertion |
| --- | --- | --- | --- | --- |
| AC-108 | `T/Middleware/SessionValidationMiddlewareTenantTests.cs` | `Switch_Recomputes_Permissions_At_Request_Time` | account limited in A, administrator in B | immediately after the switch — with no re-sign-in and no token expiry — a B-administrator-only call returns 200, and the same call before the switch returned 403 |
| AC-109 | `T/…/Tenants/TenantSwitchTests.cs` | `Stale_Tenant_Authorization_Is_Refused` | token minted while a member, membership then revoked | the same token's carried role/permission claims are **not** honoured: 403 `tenantMembershipRevoked`, and the identical request returned 200 before revocation |
| AC-109 | `T/Features/Identity/Core/AuthTokenServiceTests.cs` *changed* | `Refresh_Preserves_The_Active_Tenant` | `AuthToken` row created for a tenant | the refreshed token's tenant claim equals the original tenant, never null and never another tenant |
| AC-138 | `T/…/Tenants/TenantSwitchTests.cs` | `Request_Supplied_Tenant_Is_Ignored` | session on tenant A; request additionally carries a tenant id as a header and as a query value naming tenant B | the response contains only tenant-A rows; changing the supplied value changes nothing |
| AC-139 | `T/…/Tenants/TenantSwitchTests.cs` | `Valid_Input` | member of two created tenants | the switch response carries a new access token, the request type exposes no credential property (`typeof(TenantSwitchRequest)` has only the tenant id), and the persisted `AuthToken` row's `TenantId` equals the newly selected tenant |
| AC-140 | `T/…/Tenants/TenantSwitchTests.cs` | `No_Active_Tenant_When_Multiple_Memberships` | `dual` | straight after sign-in `GetInfoEndpoint` reports no active tenant and a tenant-scoped call returns `noActiveTenant` |
| AC-148 | `T/…/Tenants/TenantSwitchTests.cs` | `Session_Tenant_Governs_The_Request` | as AC-138 plus a revoked membership | request-supplied tenant is ignored **and** a session naming a tenant the caller no longer belongs to is refused with `tenantMembershipRevoked` |
| AC-149 | `T/…/Tenants/TenantSwitchTests.cs` | `Selection_Grants_Exactly_That_Tenants_Data` | account in two created tenants, each holding one row the test created | before selection the call is refused; after selecting A the response contains A's row and not B's |
| AC-123 | `T/…/Tenants/TenantSwitchTests.cs` | `Single_Membership_Is_Auto_Selected` | `CreateTenantUserAsync()` (exactly one membership) | straight after sign-in `GetInfoEndpoint` reports that tenant active and a tenant-scoped call returns 200 with no switch performed |

### Data isolation — AC-030..AC-037

All rows in this section use `TenancyTestsBase.TenantScopedAsync` and two tenants the test created.

| AC | File | Test method | Fixture | Failing assertion |
| --- | --- | --- | --- | --- |
| AC-030 | `T/Features/Tenancy/Core/TenantFilterTests.cs` | `Attribution_Is_Applied_On_Save` | `BeginTenant(tenantA)`, entity added with `TenantId` left unset | the persisted row's `TenantId == tenantA` — the caller supplied nothing |
| AC-031 | `T/Features/Tenancy/Core/TenantFilterTests.cs` | `Reads_Are_Restricted_To_The_Active_Tenant` | one row in each of two created tenants | under `BeginTenant(tenantA)`: `ToListAsync`, `FirstOrDefaultAsync`, `CountAsync` and `AnyAsync` all exclude tenant B's row |
| AC-032 | `T/Features/Tenancy/Core/TenantIsolationTests.cs` | `Cross_Tenant_Record_Responds_As_Missing` | a record created in tenant A, caller acting in tenant B | `GET`, `PUT` and `DELETE` by that id all return the same 404 shape as a random `Guid`, and the record's stored values are unchanged afterwards |
| AC-033 | `T/Features/Tenancy/Core/TenantFilterTests.cs` | `Bulk_Statements_Are_Restricted` | rows in both tenants | `ExecuteUpdateAsync` and `ExecuteDeleteAsync` under `BeginTenant(tenantA)` report the tenant-A row count and leave tenant B's rows untouched |
| AC-033 | `T/…/Notifications/NotificationMarkAllAsReadTests.cs` *changed* | `Raw_Statement_Respects_The_Tenant` | notifications for one recipient in two created tenants | after mark-all-as-read in tenant A, tenant B's notification for the same user is still unread |
| AC-034 | `T/Features/Tenancy/Core/TenantFilterTests.cs` | `Tenant_And_Soft_Delete_Filters_Coexist` | one soft-deleted row and one live row in tenant A, one live row in tenant B | a plain query returns only the live tenant-A row; `AcrossAllTenants()` returns both tenants' live rows and **still not** the soft-deleted one |
| AC-035 | `T/Features/Tenancy/Core/BackgroundTenantScopeTests.cs` | `Background_Work_Requires_An_Explicit_Tenant` | a scope resolved from `App.Services` with **no** HTTP request | writing an `ITenantScoped` entity while the context is unresolved throws, and the same write inside `BeginTenant(tenantA)` succeeds with `TenantId == tenantA` |
| AC-036 | `T/Features/Tenancy/Core/TenantFilterTests.cs` | `AcrossAllTenants_Is_The_Named_Opt_Out` | rows in two tenants plus one soft-deleted row | `AcrossAllTenants()` returns both tenants' rows and excludes the soft-deleted one — relaxing tenancy alone, by name |
| AC-037 | `T/Features/Tenancy/Core/TenantFilterTests.cs` | `Unresolved_Scope_Fails_Rather_Than_Returning_Everything` | unresolved `ITenantContext` | querying an `ITenantScoped` set throws; the test asserts it does **not** return rows of any tenant |
| AC-131 | `T/Features/Tenancy/Core/BackgroundTenantScopeTests.cs` | `Job_Without_A_Tenant_Fails_And_With_One_Processes_Exactly_That_Tenant` | the recurring-job entry point invoked directly, rows in two created tenants | invocation without a tenant throws; invocation with tenant A processes tenant A's rows and leaves tenant B's untouched — neither "all tenants" nor "no rows" |
| AC-080 | `T/Features/Tenancy/Core/TenantFilterTests.cs` | `Unattributed_Write_Is_Rejected` | unresolved context | `SaveChangesAsync` throws and no row is written for the entity |

### Roles and permissions — AC-038..AC-047, AC-110..AC-117

| AC | File | Test method | Fixture | Failing assertion |
| --- | --- | --- | --- | --- |
| AC-038 | `T/…/Roles/RoleCreateTests.cs` *changed* | `Same_Role_Name_In_Two_Tenants` | two created tenants, an administrator in each | both creates return 200 with different ids, and both rows persist with the same `NameNormalized` and different `TenantId` |
| AC-039 | `T/…/Roles/RoleCreateTests.cs` *changed* | `Duplicate_Name_In_Same_Tenant_Ignoring_Case` | one created tenant | code `roleNameAlreadyExists` and the role count for that tenant is unchanged |
| AC-039 | `T/Features/Tenancy/Core/TenantUniquenessTests.cs` | `Deleted_Role_Name_Is_Still_Reserved` | role created then deleted in a created tenant | re-creating that name returns `roleNameAlreadyExists` |
| AC-040 | `T/…/Permissions/GetDefinePermissionsTests.cs` | `Catalogue_Is_Global_And_Not_Extensible` | platform admin acting in two different tenants | the returned permission-name set is identical in both, and equals the code-declared set from `IPermissionDefinitionService` |
| AC-041 | `T/…/Roles/ChangePermissionsTests.cs` *changed* | `Platform_Permission_Cannot_Be_Granted_To_A_Tenant_Role` | tenant role from `CreateTenantRoleAsync()` | code `platformPermissionNotGrantable` and the role's permission set is unchanged |
| AC-042 | `T/…/Tenants/TenantCreateTests.cs` | `Provisions_A_System_Created_Administrator_Role` | fresh tenant from the create endpoint | the new tenant has exactly one `SystemCreated` role, its permission-name set equals every non-platform permission, and the tenant's first member holds it |
| AC-043 | `T/…/Roles/ChangePermissionsTests.cs` *changed* | `Cannot_Change_System_Created_Tenant_Role_Permissions` | the created tenant's administrator role | code `systemCreatedRolePermissionsCannotBeChanged` and the permission set is unchanged |
| AC-043 | `T/…/Roles/RoleDeleteTests.cs` *changed* | `Cannot_Delete_System_Created_Tenant_Role` | same | code `systemCreatedRoleCannotBeDeleted` and the role still resolves |
| AC-044 | `T/…/Tenants/TenantPermissionTests.cs` | `Every_Tenant_Endpoint_Declares_Its_Permission` | reflection over the endpoint types in `Backend.Features.Tenancy.Endpoints` | each endpoint's definition carries the expected `Allow.*` constant; adding an endpoint with no permission, or changing a constant's value, fails |
| AC-044 | `W/allow.test.ts` | `mirrors the backend tenant permission names` | none | the `Allow` map contains `Tenant_View: 'Tenant.View'` … `Platform_Administration: 'Platform.Administration'` with exactly those string values — the same literals the backend test pins |
| AC-045 | `T/…/Tenants/TenantPermissionTests.cs` | `Missing_Permission_Is_Forbidden` | `[Theory]` per tenant endpoint, caller = `TestUsers.LimitedUserId` (holds `Tenant.View` only) | every gated endpoint returns 403 with the standard forbidden shape; a caller holding the permission returns non-403 |
| AC-088 | `T/…/Tenants/TenantPermissionTests.cs` | `Purpose_Built_Role_Proves_The_Gate` | `TestRoles.LimitedTenantRoleId` — a role holding exactly one permission, never one of the all-permission seeded roles | if the endpoint's `Permissions(...)` is removed the 403 becomes 200 and the test fails |
| AC-046 | `T/…/Tenants/TenantListTests.cs` | `Platform_Administrator_Sees_Every_Tenant` | two tenants created by the test, platform admin token | both created tenants appear (`Should().Contain`), asserted by id rather than by count |
| AC-046 | `T/Features/Tenancy/Core/PlatformSurfaceTests.cs` | `Platform_Administrator_Administers_A_Tenant_It_Is_Not_A_Member_Of` | created tenant with no platform-admin membership | member management succeeds for the platform admin and the membership row appears |
| AC-047 | `T/HangfireAuthorizationFilterTests.cs` | `Dashboard_Requires_Platform_Administration` | `ClaimsPrincipal` factories: unauthenticated, tenant `Admin` role only, platform permission | `HangfireAuthorizationFilter.IsAuthorized` returns false for the first two and true only for the third — a tenant-defined role named `Admin` does not open it |
| AC-110 | `T/…/Roles/RoleListTests.cs` *changed* | `Roles_Are_Restricted_To_The_Active_Tenant` | roles created in two tenants | acting in A the response contains A's role ids, none of B's, and not the platform `Admin` role |
| AC-111 | `T/…/Roles/RoleGetTests.cs`, `RoleUpdateTests.cs`, `RoleDeleteTests.cs`, `ChangePermissionsTests.cs` *changed* | `Cross_Tenant_Role_Responds_As_Missing` (one per file) | role in tenant B, caller acting in tenant A | each returns the same 404 shape as a random `Guid`, and the role's name and permission set are unchanged afterwards |
| AC-112 | `T/…/Roles/RoleCreateTests.cs` *changed* | `Role_Is_Attributed_To_The_Active_Tenant` | caller acting in a created tenant | the request type carries no tenant property and the stored role's `TenantId` equals the active tenant |
| AC-113 | `T/…/Roles/RoleListTests.cs` *changed* | `Platform_Administrator_Sees_Roles_Across_Tenants` | roles created in two tenants | both ids appear for the platform admin |
| AC-114 | `T/…/Permissions/GetDefinePermissionsTests.cs` | `Tenant_Caller_Receives_Only_Tenant_Permissions` | tenant administrator token | the returned names contain no permission in `GetPlatformPermissionNames()` |
| AC-115 | `T/…/Permissions/GetDefinePermissionsTests.cs` | `Platform_Caller_Receives_The_Whole_Catalogue` | platform admin token | the platform permissions are present **and** flagged (`isPlatform == true`), so the two tiers are distinguishable |
| AC-116 | `T/Middleware/SessionValidationMiddlewareTenantTests.cs` | `Role_Assignment_Change_Applies_On_Next_Request` | member holding a role granting an endpoint's permission; assignment removed mid-session | the next request on the unchanged token returns 403, with no sign-in and no token expiry |
| AC-117 | `T/Middleware/SessionValidationMiddlewareTenantTests.cs` | `Role_Permission_Change_Applies_On_Next_Request` | tenant role whose permission is revoked, and a second case where the role is deleted | the holder's next request returns 403 in both cases on the unchanged token |

### Identity and sign-in — AC-048..AC-051, AC-093..AC-096, AC-118..AC-122

| AC | File | Test method | Fixture | Failing assertion |
| --- | --- | --- | --- | --- |
| AC-048 | `T/…/Users/UserCreateTests.cs` *changed* | `Username_And_Email_Stay_Globally_Unique` | account created while acting in tenant A | creating the same username while acting in tenant B returns `usernameAlreadyExists` (and the email case returns `emailAlreadyExists`) |
| AC-049 | `T/Features/Identity/Endpoints/Account/TokenTests.cs` | `Sign_In_Does_Not_Name_A_Tenant` | seeded account | `typeof(TokenRequest)` exposes no tenant property, and the call succeeds with username and password alone |
| AC-050 | `T/Features/Tenancy/Core/TenantContextResolutionTests.cs` | `Account_With_No_Membership_Is_Refused_With_An_Explanation` | `TestUsers.NoMembershipUserId` | every tenant-scoped call returns 403 with code `noActiveTenant`; the body carries the code so the client can translate it |
| AC-051 | `T/Features/Identity/Endpoints/Account/AccountSelfServiceTests.cs` | `Self_Service_Flows_Work_Without_A_Tenant` | `[Theory]` over sign-up, resend-verify-email, forgot-password, change-password, profile read, profile update, profile image upload — caller `nomember` | every one returns success and none returns `noActiveTenant` |
| AC-051 | `T/…/Account/ChangePasswordTests.cs` *changed* | `Works_Without_An_Active_Tenant` | `nomember` | 200 rather than `noActiveTenant` |
| AC-093 | `T/…/Users/UserListTests.cs` *changed* | `Users_Are_Restricted_To_The_Active_Tenant` | accounts created in two tenants | acting in A the ids contain A's account and not B's; the count endpoint's total does not include B's |
| AC-094 | `T/…/Users/UserGetTests.cs`, `UserUpdateTests.cs`, `UserDeleteTests.cs` *changed* | `Cross_Tenant_User_Responds_As_Missing` (one per file) | account in tenant B, caller acting in tenant A | same 404 shape as a random `Guid`, and the account's stored values and active flag are unchanged |
| AC-095 | `T/…/Users/UserListTests.cs` *changed* | `Platform_Administrator_Sees_Every_Account` | accounts created in two tenants | both ids appear for the platform admin |
| AC-096 | `T/…/Users/UserCreateTests.cs` *changed* | `Created_User_Gains_A_Membership_In_The_Active_Tenant` | caller acting in a created tenant | a `TenantMembership` row exists for the new account in that tenant, and the account is visible in that tenant's user list |
| AC-118 | `T/…/Account/SignupTests.cs` | `Creates_A_Global_Account_With_No_Membership` | anonymous client | the account exists and `DbContext.TenantMemberships.AcrossAllTenants().Any(m => m.UserId == id)` is false |
| AC-119 | `T/…/Account/SignupTests.cs` | `Works_With_No_Tenant_Context` | `ClearAuthToken()` | 200 rather than `noActiveTenant` |
| AC-120 | `T/…/Account/SignupTests.cs` | `Grants_No_Role_And_No_Platform_Permission` | the signed-up account | it holds zero `UserRole` rows, and its token's permission claims are empty |
| AC-121 | `T/Features/Tenancy/Core/TenantSeedingTests.cs` | `Every_Role_Has_A_Declared_Scope` | seeded database | every role read with `AcrossAllTenants()` either has a non-null `TenantId` or is a `SystemCreated` platform role holding at least one platform permission — no role is scopeless |
| AC-122 | `T/…/Account/SignupTests.cs` | `New_Account_Is_An_Authenticated_User_With_No_Usable_Membership` | signed-up account's own token | `UserListEndpoint` returns 403 `noActiveTenant` while `ProfileEndpoint` and `TenantOnboardEndpoint` return 200 |

### Notifications — AC-052..AC-056

| AC | File | Test method | Fixture | Failing assertion |
| --- | --- | --- | --- | --- |
| AC-052 | `T/…/Notifications/NotificationTenancyTests.cs` | `Tenant_Notification_Is_Visible_Only_In_Its_Tenant` | one recipient in two created tenants, one notification raised in A with a `Guid`-unique title key | acting in A the key is present; acting in B it is absent |
| AC-053 | `T/Features/Notifications/Core/NotificationServiceTests.cs` | `Addresses_A_Single_Member` / `Addresses_Every_Member_Of_The_Tenant` | two members of a created tenant | the single-member call produces a row only for that member; the tenant-wide call is visible to both members and to nobody in another created tenant |
| AC-054 | `T/…/Notifications/NotificationTenancyTests.cs` | `Platform_Wide_Notification_Is_Visible_In_Every_Tenant` | platform-wide notification with a unique key | the key appears while acting in both created tenants, and its stored `TenantId` is null so it is distinguishable from a tenant-wide one |
| AC-055 | `T/…/Notifications/NotificationMarkAllAsReadTests.cs` *changed* | `Marks_Only_The_Active_Tenants_Notifications` | unread notifications for one recipient in two created tenants | after the call in tenant A, tenant A's row is read and tenant B's row is still unread |
| AC-056 | `T/…/Notifications/NotificationGetUnreadCountTests.cs` *changed* | `Counts_Only_The_Active_Tenant` | as above | the count taken in A before and after adding an unread notification in B is unchanged (delta asserted, never an absolute count) |
| AC-129 | `T/…/Notifications/NotificationTenancyTests.cs` | `Notification_In_One_Tenant_Is_Neither_Listed_Nor_Counted_In_Another` | as AC-052 plus a platform-wide notification | tenant-A notification absent from B's list and from B's unread-count delta, while the platform-wide one is present in both |
| AC-130 | `T/…/Notifications/NotificationMarkAllAsReadTests.cs` *changed* | `Raw_Statement_Leaves_Other_Tenants_Unread` | the `ExecuteUpdateAsync` path and the `ON CONFLICT` visit-insert path, exercised for the same recipient in two tenants | after mark-all in A: A's rows read, B's rows unread, and no `NotificationVisit` row was written for a notification outside tenant A |

### File storage — AC-057..AC-060, AC-097..AC-099

| AC | File | Test method | Fixture | Failing assertion |
| --- | --- | --- | --- | --- |
| AC-057 | `T/…/Files/FileUploadTests.cs` | `Upload_Is_Attributed_To_The_Active_Tenant` | member of a created tenant, small in-memory file | the `StoredFile` row's `TenantId` equals the active tenant and its `OwnerUserId` is null for a tenant-scoped upload |
| AC-058 | `T/…/Files/FileGetTests.cs` | `Cross_Tenant_File_Is_Refused` | file uploaded in tenant A, caller acting in tenant B | 403 with code `crossTenantFileAccess` and the response body carries no file content |
| AC-058 | `T/…/Files/FileDeleteTests.cs` | `Cross_Tenant_Delete_Is_Refused` | same | same code, and the `StoredFile` row and the physical file both still exist |
| AC-059 | `T/…/Files/FileGetTests.cs` | `Guessed_Stored_Name_Does_Not_Yield_Another_Tenants_Content` | the **actual** stored name read from tenant A's `StoredFile` row | the request from tenant B returns `crossTenantFileAccess` and zero content bytes |
| AC-060 | `T/…/Tenants/TenantSuspensionTests.cs` | `Suspended_Tenant_Files_Are_Not_Served_But_Are_Retained` | file uploaded before suspension | the download is refused after suspension while the `StoredFile` row (read with `AcrossAllTenants()`) and the stored bytes still exist |
| AC-097 | `T/Features/FileManagement/Core/FileTenancyTests.cs` | `Account_Owned_File_Is_Readable_In_Any_Tenant_And_In_None` | profile image uploaded by `dual`; then acting in tenant A, in tenant B, and as `nomember` | the row's `TenantId` is null with `OwnerUserId` set, and all three reads return the content |
| AC-098 | `T/…/Files/FileUploadTests.cs`, `FileGetTests.cs`, `FileDeleteTests.cs` | `Unauthenticated` (one per file) | `ClearAuthToken()` | all three return 401 — the endpoints are no longer anonymously reachable |
| AC-099 | `T/…/Files/FileUploadTests.cs` | `Upload_Without_An_Active_Tenant_Is_Refused` | `dual` signed in without switching | 403 `noActiveTenant`, no `StoredFile` row for the submitted name, and no file written to the storage directory |
| AC-128 | `T/Features/FileManagement/Core/FileTenancyTests.cs` | `Member_Of_One_Tenant_Cannot_Read_Replace_Or_Delete_Another_Tenants_File` | file in tenant A, member of tenant B, plus the account-owned case above | read, replace and delete each return `crossTenantFileAccess`, including when the stored file name is supplied verbatim; the account-owned image stays readable in both tenants and with none active |

### Lists, paging, sorting and search — AC-061..AC-066

| AC | File | Test method | Fixture | Failing assertion |
| --- | --- | --- | --- | --- |
| AC-061 | `T/…/Tenants/TenantListTests.cs` | `List_Tenants_Pagination` | three tenants created by the test, filtered to them by `Search` on their shared unique prefix | `PageSize = 2` returns 2 rows and page 2 returns the third; `PageSize` above the documented maximum is rejected with a 400 on `pageSize` |
| AC-061 | `T/…/Tenants/TenantMemberListTests.cs` | `List_Members_Pagination` | one created tenant with three members | same shape, scoped to the created tenant |
| AC-062 | `T/…/Tenants/TenantListTests.cs` | `Sorting` | tenants created with deterministic names | ascending and descending on `name` and on `identifier` produce reversed sequences of the created ids |
| AC-062 | `T/…/Tenants/TenantMemberListTests.cs` | `Sorting` | created members | same for the member list's whitelisted fields |
| AC-063 | `T/…/Tenants/TenantListTests.cs` | `Invalid_Sort_Field` | `SortField = "password"` | 400 with error name `sortField`; no rows returned |
| AC-063 | `T/…/Tenants/TenantMemberListTests.cs` | `Invalid_Sort_Field` | same | 400 with error name `sortField` |
| AC-064 | `T/…/Tenants/TenantListTests.cs` | `Search_By_Name_And_Identifier` | tenant created with a `Guid`-unique name and identifier | searching either value returns exactly that tenant; searching a differently-cased identifier still finds it (normalized comparison) |
| AC-064 | `T/…/Tenants/TenantMemberListTests.cs` | `Search_By_Username_And_Email` | member with a unique username and email | both searches return exactly that member |
| AC-065 | `T/…/Tenants/TenantListTests.cs` | `Filter_By_Status` | one active and one suspended tenant created by the test | filtering by suspended returns the suspended id and not the active one |
| AC-066 | `T/…/Tenants/TenantListTests.cs` | `Non_Platform_Caller_Sees_Only_Own_Tenants` | member of one created tenant, plus a second created tenant it does not belong to | the response contains the member's tenant and **not** the other, even though the other exists |

### Errors and messages — AC-067..AC-070

| AC | File | Test method | Fixture | Failing assertion |
| --- | --- | --- | --- | --- |
| AC-067 | `T/ErrorHandling/TenantErrorCodesTests.cs` | `All_Tenant_Error_Codes_Are_Declared` | reflection over `ErrorCodes` constants | the declared values contain exactly `tenantNotFound`, `tenantSuspended`, `notTenantMember`, `noActiveTenant`, `tenantIdentifierAlreadyExists`, `duplicateTenantMembership`, `lastTenantAdministrator`, `systemCreatedTenantCannotBeModified`, `crossTenantFileAccess`, `tenantMembershipRevoked`, `platformPermissionNotGrantable`, `concurrentModification` — renaming or dropping one fails |
| AC-068 | every negative endpoint test above | — | — | each asserts `res.Errors.Single().Code` and, for field-attributable failures (`identifier`, `name`, `sortField`, `pageSize`), `res.Errors.Single().Name`; a response that omitted the code or the field name would fail |
| AC-069 | `W/i18n/locales.test.ts` | `defines a message for every tenant error code in every locale` | the eight files under `public/locales/`, read with `fs` | each of the twelve codes listed in the test has a non-empty `error.server.<code>` in all eight locales; a missing or empty value fails |
| AC-069 | `W/lib/utils/api-error-helpers.test.ts` | `translates a tenant error code instead of showing it raw` | fake `t` returning `translated:<key>` for known keys and the key itself for unknown ones | `getApiErrorMessages` on a 403 carrying `tenantSuspended` returns the translated message, and never a string equal to the raw code or the raw key |
| AC-070 | `T/…/Tenants/TenantSuspensionTests.cs` | `Refusal_Explains_And_Keeps_The_Session` | member of two created tenants, one suspended, one membership revoked | both refusals carry a distinct code (`tenantSuspended`, `tenantMembershipRevoked`) with 403 rather than 401, and `GetInfoEndpoint` on the same token still lists an alternative tenant |
| AC-070 | `W/lib/utils/tenant-routing.test.ts` | `offers another tenant instead of signing the user out` | `userInfo` whose active tenant is suspended and which has a second membership | `resolveTenantLanding` returns `'/select-tenant'`, never a sign-out route |

### Web experience and localization — AC-071..AC-077, AC-123..AC-127, AC-141..AC-143

| AC | File | Test method | Fixture | Failing assertion |
| --- | --- | --- | --- | --- |
| AC-071 | `W/auth-urls.test.ts` | `gates every tenant screen by the permission its operation requires` | `authUrls` | `getMatchedAuthUrl` for the list, create, update, suspend/reactivate and delete routes returns exactly `[Allow.Tenant_View]`, `[Allow.Tenant_Create]`, `[Allow.Tenant_Update]`, `[Allow.Tenant_Suspend]`/`[Allow.Tenant_Reactivate]`, `[Allow.Tenant_Delete]` |
| AC-072 | `W/auth-urls.test.ts` | `gates the tenant members screen` | `authUrls` | the members route resolves to `[Allow.TenantMember_View]` |
| AC-074 | `W/auth-urls.test.ts` | `refuses direct navigation to a tenant screen without its permission` | `isAllowed` + `getMatchedAuthUrl` | for a state lacking the permission, `isAllowed(state, getMatchedAuthUrl(route).permissions)` is false for each tenant route; every tenant entry in `searchableItems` has a matching guarded `authUrls` entry, so no search entry can point at an ungated route |
| AC-141 | `W/auth-urls.test.ts` | `gates platform screens by the platform permission` | `authUrls` | every platform route resolves to `[Allow.Platform_Administration]` |
| AC-143 | `W/auth-urls.test.ts` | `keeps the no-tenant and create-tenant screens reachable by any authenticated user` | `authUrls` | both entries exist and carry no `permissions`, so an account with no membership is not locked out of them |
| AC-076 | `W/i18n/locales.test.ts` | `keeps every locale file at key parity with English` | the eight locale files and `i18nConfig.locales` | the flattened key set of each locale equals `en.json`'s exactly — a key added to English only fails the test |
| AC-123 | `W/store/slices/authSlice.test.ts` | `stores the active tenant supplied by get-info` | `userInfo({ tenants: [one], activeTenant: one })` | after `setUserInfo`, `state.user.activeTenant` is that tenant; the reducer does not invent or clear it |
| AC-124 | `T/Features/Identity/Endpoints/Account/GetInfoTests.cs` | `Active_Tenant_Survives_A_Fresh_Client` | the same access token used from a second `HttpClient` (a new tab's equivalent) | the reported active tenant is identical, proving the selection lives in the session and not in browser state |
| AC-125 | `W/store/tenant-cache.test.ts` | `discards the selection and the cached data on sign-out` | none | `signedOutActions()` contains `appApi.util.resetApiState()` and the `signout` action, with the reset first |
| AC-125 | `T/…/Account/SignoutTests.cs` | `Signout_Clears_The_Session_Tenant` | signed-in member of a created tenant | after sign-out the refresh token row is gone and a following request has no active tenant |
| AC-126 | `W/lib/utils/tenant-routing.test.ts` | `routes an account with no usable membership to the no-tenant screen` | `userInfo({ tenants: [] })` | `resolveTenantLanding` returns `'/no-tenant'`, never a tenant-scoped route |
| AC-127 | `W/lib/utils/tenant-routing.test.ts` | `discards a stale active-tenant selection` | `[each]` active tenant suspended, deleted (absent from the list), membership removed | `isActiveTenantStale` is true and `resolveTenantLanding` returns `'/select-tenant'` |
| AC-127 | `T/Middleware/SessionValidationMiddlewareTenantTests.cs` | `Stale_Selection_Is_Not_Honoured` | member whose active tenant is suspended mid-session | the next tenant-scoped request is refused and `GetInfoEndpoint` reports no active tenant, so the client is required to choose again |
| AC-142 | `W/lib/utils/tenant-routing.test.ts` | `sends a multi-tenant user to the selection screen` | `userInfo({ tenants: [a, b], activeTenant: undefined })` | `resolveTenantLanding` returns `'/select-tenant'` — never `null`, which would open a tenant-scoped screen |
| AC-073 | `W/store/slices/authSlice.test.ts` | `exposes the tenant list the chrome switches between` | `userInfo({ tenants: [a, b] })` | `state.user.tenants` holds both, so the switcher has data; **the rendering half is not automatically tested — see below** |
| AC-075 | — | — | — | **not automatically tested — see below** |
| AC-077 | — | — | — | **not automatically tested — see below** |
| AC-092 | `W/lib/utils/authentication-and-authorization.test.ts` *changed* | `evaluates permissions for the tenant the user is acting in` | `stateWithPermissions` extended with a tenant argument: administrator permissions in tenant B, `Tenant.View` only in tenant A | `isAllowed` is false for `Tenant.Create` while the state carries tenant A's permissions and true while it carries tenant B's — the same user, two answers |
| AC-132 | `W/store/tenant-cache.test.ts` | `proves no previous tenant record survives a switch` | ordered action array | `tenantChangedActions` starts with `appApi.util.resetApiState()`; removing the reset leaves the array without it and the test fails |

### Persistence, audit and concurrency — AC-078..AC-081, AC-144

| AC | File | Test method | Fixture | Failing assertion |
| --- | --- | --- | --- | --- |
| AC-078 | `T/…/Tenants/TenantCreateTests.cs` | `Records_Audit_Fields` | see AC-012 | `CreatedBy`/`CreatedAt` set on the tenant, `UpdatedBy`/`UpdatedAt` set after an update |
| AC-078 | `T/…/Tenants/TenantMemberAddTests.cs` | `Records_Audit_Fields` | member added by the seeded admin | the membership row's `CreatedBy` is the acting user and `CreatedAt` is within the test window |
| AC-078 | `T/…/Roles/RoleCreateTests.cs` *changed* | `Records_Audit_Fields` | role created in a created tenant | same for the tenant role |
| AC-079 | `T/…/Tenants/TenantDeleteTests.cs` | `Delete_Tenant` | see AC-009 | row retained under `IgnoreQueryFilters()` and excluded from a normal query |
| AC-079 | `T/…/Tenants/TenantMemberRemoveTests.cs` | `Removal_Is_Soft` | removed member | the membership row is retained with `IsDeleted` true and excluded from the member list |
| AC-080 | `T/Features/Tenancy/Core/TenantFilterTests.cs` | `Unattributed_Write_Is_Rejected` | see the isolation section | `SaveChangesAsync` throws rather than storing a row with a null `TenantId` |
| AC-081 | `T/Features/Tenancy/Core/TenantConcurrencyTests.cs` | `Concurrent_Assignment_Updates_Keep_One_Complete_Set` | two `ClientForAsync` clients issuing role-assignment replacements `{A,B}` and `{C,D}` for the same member concurrently | exactly one call succeeds, the other returns `concurrentModification`, and the member's final assignment set equals `{A,B}` **or** `{C,D}` — never a mixture such as `{A,D}` |
| AC-144 | `T/Features/Tenancy/Core/TenantUniquenessTests.cs` | `Uniqueness_Is_Enforced_By_The_Database_Over_Retained_Rows` | direct `DbContext` insert bypassing the service, for both a soft-deleted tenant identifier and a deleted tenant role name | `SaveChangesAsync` throws `DbUpdateException` whose constraint name is `IX_Tenants_Identifier` / `IX_Roles_TenantId_Name` — proving the constraint exists in the database, not only in application code |
| AC-144 | `T/Features/Tenancy/Core/TenantUniquenessTests.cs` | `Platform_Role_Names_Are_Unique` | two platform-scoped roles (`TenantId == null`) with the same name inserted directly | the second insert is refused — the `NULLS NOT DISTINCT` behaviour; without it PostgreSQL would accept both |
| AC-147 | `T/Features/Tenancy/Core/TenantUniquenessTests.cs` | `Deleted_Names_Are_Refused_As_Duplicates` | one soft-deleted tenant, one deleted tenant role | re-using either name through the endpoints returns `tenantIdentifierAlreadyExists` / `roleNameAlreadyExists` |

### Bootstrap and seeding — AC-082..AC-085, AC-145

| AC | File | Test method | Fixture | Failing assertion |
| --- | --- | --- | --- | --- |
| AC-082 | `T/Features/Tenancy/Core/TenantSeedingTests.cs` | `Bootstrap_Tenant_Exists_With_The_Seeded_Administrator` | the seeded database (`SharedContextFixture` migrated it from empty) | exactly one `SystemCreated` tenant exists, its id is `TenancyConstants.BootstrapTenantId`, and the seeded `admin` account holds an active membership in it with tenant administration |
| AC-083 | `T/Features/Tenancy/Core/TenantSeedingTests.cs` | `Seeded_Administrator_Is_A_Platform_Administrator` | seeded database | `admin` holds `Allow.Platform_Administration`, and the role granting it has `TenantId == null` — so no tenant role can confer it |
| AC-084 | `T/Features/Tenancy/Core/TenantSeedingTests.cs` | `Reconciliation_Preserves_Tenants_Memberships_And_Tenant_Roles` | a tenant, membership and tenant role created by the test, then `DataSeeder` run again from `App.Services` | all three still exist with the same ids and the tenant role keeps its permissions |
| AC-085 | `T/Features/Tenancy/Core/TenantSchemaTests.cs` | `Schema_Is_Complete_From_A_First_Time_Creation` | the database `SharedContextFixture` migrated from empty | `GetPendingMigrationsAsync()` is empty and the tenancy tables and both unique indexes are present — the suite only runs at all because the schema builds from scratch |
| AC-145 | `T/Features/Tenancy/Core/TenantSeedingTests.cs` | `No_Public_Role_Is_Seeded` | seeded database | no role named `Public` exists at any scope, and no role exists that sign-up would auto-grant |

### Test-coverage criteria — AC-086..AC-092

| AC | Discharged by | Failing condition |
| --- | --- | --- |
| AC-086 | the thirteen `T/Features/Tenancy/Endpoints/Tenants/*Tests.cs` files, each with a `Valid_Input` plus one method per failure branch listed above | a tenant or membership operation whose failure branch has no method — `/verify` reads the branch list in this document against the files |
| AC-087 | `T/Features/Tenancy/Core/TenantIsolationTests.cs` — `Cross_Tenant_Record_Responds_As_Missing`, `Cross_Tenant_Record_Is_Not_Listed`, `Cross_Tenant_Update_Is_Refused`, `Cross_Tenant_Delete_Is_Refused` | any of the four verbs succeeding across tenants |
| AC-088 | `T/…/Tenants/TenantPermissionTests.cs` with `TestRoles.LimitedTenantRoleId` | the test using an all-permission seeded role instead, which would make the 403 unprovable |
| AC-089 | `T/…/Tenants/TenantSuspensionTests.cs` — refusal then restoration on the same token | suspension not refusing, or reactivation requiring a new sign-in |
| AC-090 | `TenancyTestsBase` factories + the assertion style used throughout (`Should().Contain`, deltas, filters to created ids) | any tenancy test asserting an absolute count, "the first row" of a global list, or mutating `admin`, `Admin`, the shared test roles or a seeded tenant |
| AC-091 | `T/Architect/TenantScopingTests.cs` — `Every_Tenant_Scoped_Entity_Carries_The_Tenant_Filter` and `Every_Entity_Is_Scoped_Or_Exempt_With_A_Reason` (reasoned dictionary covering `User`, `Permission`, `RolePermission`, `UserRole`, `AuthToken`, `Token`, `NotificationVisit`, `Tenant`) | adding a persisted entity that is neither `ITenantScoped` nor listed with a written reason |
| AC-092 | `W/lib/utils/authentication-and-authorization.test.ts` — `evaluates permissions for the tenant the user is acting in` | permission evaluation that ignores which tenant the state came from |

## Negative-test checklist

Every unwanted-behaviour criterion (`If … then the system shall …`) and every state-driven refusal
has a test that asserts the refusal, the code, and that nothing was written or changed:

AC-003, AC-004, AC-007, AC-010, AC-011, AC-015, AC-016, AC-019, AC-021, AC-023, AC-026, AC-029,
AC-032, AC-037, AC-039, AC-041, AC-043, AC-045, AC-047, AC-050, AC-058, AC-063, AC-070, AC-074,
AC-080, AC-094, AC-098, AC-099, AC-104, AC-109, AC-111, AC-127, AC-136 — each appears in the matrix
above with a refusal assertion. Where the criterion says "respond exactly as for a record that does
not exist" (AC-032, AC-094, AC-111), the test asserts the cross-tenant response and the random-`Guid`
response are the **same** status and body shape, so a distinct "forbidden" reply would fail.

## Criteria not covered automatically

| AC | Why not | What covers it instead |
| --- | --- | --- |
| AC-073 (rendering half) | the active tenant appearing in the application chrome is component rendering, which the frontend suite excludes by design (`frontend-tests`: "not worth it here — component rendering") | `authSlice.test.ts` proves the state the chrome reads; the visual placement is a review item on the `header.tsx` / `tenant-switcher.tsx` task |
| AC-074 (navigation and search omission half) | `nav-items.ts` imports `lucide-react` components, so asserting on it drags rendering dependencies into a suite with no DOM environment | the guard half is fully tested via `authUrls`; `searchable-items.ts` (pure data) is asserted to have a guarded entry for every tenant route; nav omission is a review item |
| AC-075 | "no hard-coded user-visible string" is a property of source text, not of behaviour | `npm run lint` plus code review of the web tasks; the locale parity test makes a missing key fail loudly, which is how a hard-coded string usually surfaces |
| AC-077 | right-to-left rendering needs a browser; adding `jsdom` for it would be a deliberate suite change this plan does not make | review against the existing logical-property class conventions, on the same task that adds each screen |
| AC-085 (generated-project half) | a newly generated project's first `dotnet ef migrations add Initial` happens outside this repository's test suite | `TenantSchemaTests.Schema_Is_Complete_From_A_First_Time_Creation` proves the model builds a complete schema from empty; the generated-project run is a `new-project` manual check |
| AC-036 (no raw opt-out call sites) | a test cannot see a call site that does not execute; a source scan from the test assembly would be path-fragile and would ship to generated projects | behaviour is tested; "`AcrossAllTenants()` is the only opt-out" is enforced by review plus a grep for `IgnoreQueryFilters` in the implementation task |
| AC-090 (as a property of the whole suite) | "tests create what they assert on" cannot be asserted from inside a test | `TenancyTestsBase` makes the correct thing the easy thing, and the rule is a review item on every test task |
| AC-124 (browser-tab half) | additional tabs and reloads are browser behaviour | `GetInfoTests.Active_Tenant_Survives_A_Fresh_Client` proves the server side, and decision D19 means there is no browser storage that could diverge |

## Parallel-safety rules for this feature

The backend suite runs against one database with `SharedContextFixture` seeded once. On top of the
`backend-tests` rules, tenancy adds these:

1. **Create your tenants.** Any test asserting on rows creates its tenants through
   `CreateTenantAsync()`. `TestTenants.BootstrapTenantId` and `SecondTenantId` are used **read-only**,
   or as the subject of a refusal (AC-011).
2. **Never suspend or delete a seeded tenant.** Suspension is global state; suspend only a tenant the
   test created. This is the single most likely way to break the suite.
3. **Never add a membership to `admin`, `test`, `testone` or `testtwo`,** or AC-123's auto-selection
   stops applying and every existing test loses its active tenant.
4. **Assert on deltas and ids, never on totals.** Unread counts, tenant lists and user lists are
   global surfaces; capture before, act, capture after, assert the difference, or filter by an id or
   a `Guid`-unique search term the test created.
5. **Two identities means two clients.** `App.Client`'s default headers are per test class; a test
   needing two callers at once uses `ClientForAsync(...)` rather than swapping the shared header.
6. **Unique everything.** Tenant identifiers, role names, usernames, emails and notification keys all
   carry `Guid.NewGuid()` or Bogus `f.UniqueIndex`; tenant identifiers must still satisfy AC-101, so
   use the `t-{guid:N}` shape from the factory.

## Deviations from plan.md

| plan.md says | This plan says | Why |
| --- | --- | --- |
| `TenantSwitchTests` and `TenantOnboardTests` live under `Tests/Features/Identity/Endpoints/Account/` | they live under `Tests/Features/Tenancy/Endpoints/Tenants/` | plan.md's own workstream 2 puts `TenantSwitchEndpoint` and `TenantOnboardEndpoint` in `Features/Tenancy/Endpoints/Tenants/`, and `backend-tests` requires the test tree to mirror the source tree |
| the test table lists thirteen endpoint test files generically | they are enumerated by name here, one per endpoint, with the failure branches each must cover | AC-086 is measured per failure branch, so the branch list has to exist somewhere `/verify` can read it |
| `authSlice.test.ts` carries AC-028/AC-132 | `store/tenant-cache.test.ts` carries them; `authSlice.test.ts` carries the auth-state half | `resetApiState` is an api-level action, not slice state; asserting it through the slice reducer would pass with the behaviour removed |
| — | `lib/utils/tenant-routing.ts` and `store/tenant-cache.ts` are required as pure modules | AC-126, AC-127, AC-132 and AC-142 are otherwise only observable through a rendered component |

## Artifacts

Every file this contract requires is listed in the two inventories above; the governing skill is
`backend-tests` for everything under `src/backend/Tests/` and `frontend-tests` for everything under
`src/frontend/web/`, except `src/backend/Source/HangfireAuthorizationFilter.cs` (governed by
`coding-conventions`) and the two new web modules (`frontend-tests` for their tests,
`coding-conventions` for the modules themselves).

## Open questions

All questions raised while these contracts were written have been answered by the requester. The
decisions, in the order they were put:

1. **First-member administrator role.** The tenant's system-created administrator role is assigned to
   the first member automatically, whether or not the add request names it, so a tenant can never
   exist without an administrator. The add request's role set is unioned with that role for the first
   member; for every later member it is taken exactly as supplied.
2. **Tenant rename tier.** The tenant update permission stays at platform tier. A tenant administrator
   manages members and roles but never renames the tenant or changes its identifier, so the update
   endpoint keeps its platform-only authorization and gains no member branch.
3. **Account-owned uploads.** The existing upload route carries a boolean field marking a file
   account-owned; there is no second route. The profile avatar upload sets it, and every other upload
   leaves it unset and is attributed to the active tenant.
4. **Identifier case clash.** A tenant identifier is trimmed and lower-cased before the uniqueness
   comparison, so submitting an identifier that differs from an existing one only in case is refused
   as a duplicate on the identifier field, not as a character-set validation failure.
5. **Assignable roles for a tenant administered from the platform.** The existing role list accepts an
   optional tenant to filter by, honoured only for a caller holding platform-tier authority and
   ignored for every other caller. There is no dedicated route for a tenant's roles, and the role
   response shape is unchanged.

Two smaller points were settled without a question, both recorded here so a reviewer sees the choice
rather than an omission. The twelve tenant error codes are listed literally in both the web locale
test and the backend error-code test rather than sharing a source, because a duplicated literal list
is the point of a guard test. The background-job dashboard refusal is proved by a unit test on the
authorization filter, without an additional end-to-end request.

