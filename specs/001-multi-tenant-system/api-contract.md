# API contract - Multi-tenant system

Spec: `specs/001-multi-tenant-system/spec.md` - Plan: `specs/001-multi-tenant-system/plan.md`

This document is the HTTP surface of the feature: every endpoint, its route group and prefix, its
request type, its validator rules, its response and DTO types, its Mapperly mappers, the permission
constant that guards it, and every failure it can produce. It also fixes the permission catalogue and
the error-code catalogue the rest of the feature reads.

It does **not** cover entities, EF configuration, the query filter, migrations or seeding (those are
`data-model.md`), the web client (`frontend-contract.md`) or the tests (`test-plan.md`). Where an
endpoint needs a service another contract owns, the surface this contract depends on is stated in
§7, so the two documents cannot drift apart.

---

## 1. Conventions inherited from the codebase

These are existing rules, restated because every endpoint below relies on them.

- **Route prefix.** A global prefix comes from configuration (`c.Endpoints.RoutePrefix`) and API
  versioning uses the `v` prefix (`c.Versioning.Prefix = "v"`). Routes here are written **relative to
  the route group**, exactly as `Configure()` declares them. Neither prefix is ever hard-coded.
- **One endpoint = one file** under
  `src/backend/Source/Features/<Feature>/Endpoints/<Area>/<Entity><Action>Endpoint.cs`, holding the
  endpoint, its request, its validator, its response and row DTOs, and its mappers, in that order
  (`backend-endpoint`).
- **Naming** follows the `coding-conventions` table: `<Entity><Action>Endpoint`, `…Request`,
  `…Validator`, `…Response`, `<Entity>ListDto`, `<Entity><Action>{Request,Response,Dto}Mapper`,
  `<Area>Group`, `<Feature>PermissionsProvider`, `Entity_Action` permission constants, camelCase
  error codes.
- **Accessibility.** No accessibility modifier unless the type must cross the assembly boundary;
  types are `sealed`; Mapperly mappers are `public partial class`; primary constructors for
  dependencies; XML doc on every type.
- **Status codes.** `ThrowError(...)` produces **400** ProblemDetails carrying `errors[].code`
  (`IndicateErrorCode = true`); `Send.NotFoundAsync` produces **404**; a failed `Permissions(...)`
  check produces the framework's **403**; an unauthenticated call to an endpoint that is not
  `AllowAnonymous()` produces **401**. No new status code is introduced (spec, "Error surface").
- **Paging** is `ListRequestDto<TId>` + `ListRequestDtoValidator<TId>` + `IQueryableExtension.Process`:
  `Page` defaults to 1, `PageSize` defaults to 10 and is bounded to 1..100, `IncludeIds` is capped at
  100 entries, `All = true` is capped at 10,000 rows by `Process`, and the response is `ListDto<T>`
  (`Items` + `Total`) with `Total` counted **before** paging. Default ordering with no `SortField` is
  `UpdatedAt DESC` for auditable entities. AC-061 is satisfied by this existing contract with no new
  mechanism.
- **Sort whitelists** live in the request validator; a field outside the whitelist fails validation
  with 400 (AC-063), because `Process` would otherwise throw.
- **Enums** serialize as strings (`JsonStringEnumConverter` in `Program.cs`), so `TenantStatus`
  crosses the wire as `"Active"` / `"Suspended"`.
- **Search** uses the normalized columns where one exists (`EF.Functions.Like` over
  `…Normalized` with `search = request.Search?.Trim().ToLowerInvariant()`), and `EF.Functions.ILike`
  over a column that has no normalized twin, as `NotificationListEndpoint` already does.

### 1.1 Two conventions this contract settles

| Question | Decision | Why |
| --- | --- | --- |
| A tenant addressed by id that the caller may not see | `ThrowError("Tenant not found", ErrorCodes.TenantNotFound)` -> **400** carrying the code, **not** `Send.NotFoundAsync` | AC-010 requires a *defined error code* and requires the response not to reveal whether the tenant ever existed. One code for "absent", "soft-deleted" and "invisible to you" delivers both, and AC-069 then has something to translate. |
| A tenant-scoped **record** (role, notification, stored file, membership) belonging to another tenant | responds exactly as for a missing row - **404** via `Send.NotFoundAsync`, produced naturally by the named `Tenant` query filter | AC-032, AC-111. Unchanged from today's behaviour, which is the point of those criteria. |

The two do not conflict: the first is about a tenant *as the addressed resource* on the
administration surface; the second is about rows *inside* a tenant.

---

## 2. Permission catalogue

### 2.1 Constants - `src/backend/Source/Permissions/Allow.cs`

Appended to the existing `Allow` class, after `File_Delete`:

| Constant | Value | Tier | Guards | Criteria |
| --- | --- | --- | --- | --- |
| `Tenant_View` | `"Tenant.View"` | tenant | `TenantListEndpoint`, `TenantGetEndpoint` | AC-046, AC-066, AC-071 |
| `Tenant_Create` | `"Tenant.Create"` | **platform** | `TenantCreateEndpoint` | AC-002, AC-071 |
| `Tenant_Update` | `"Tenant.Update"` | **platform** | `TenantUpdateEndpoint` | AC-005, AC-071 |
| `Tenant_Suspend` | `"Tenant.Suspend"` | **platform** | `TenantSuspendEndpoint` | AC-006, AC-071 |
| `Tenant_Reactivate` | `"Tenant.Reactivate"` | **platform** | `TenantReactivateEndpoint` | AC-008, AC-071 |
| `Tenant_Delete` | `"Tenant.Delete"` | **platform** | `TenantDeleteEndpoint` | AC-009, AC-071 |
| `TenantMember_View` | `"TenantMember.View"` | tenant | `TenantMemberListEndpoint` | AC-021, AC-072 |
| `TenantMember_Add` | `"TenantMember.Add"` | tenant | `TenantMemberAddEndpoint` | AC-014, AC-072 |
| `TenantMember_UpdateRoles` | `"TenantMember.UpdateRoles"` | tenant | `TenantMemberUpdateRolesEndpoint` | AC-017, AC-072 |
| `TenantMember_Remove` | `"TenantMember.Remove"` | tenant | `TenantMemberRemoveEndpoint` | AC-018, AC-072 |
| `Platform_Administration` | `"Platform.Administration"` | **platform** | the Hangfire dashboard, and read as the *tier test* by every endpoint that widens its rows for a platform administrator | AC-041, AC-046, AC-047, AC-083, AC-095, AC-113, AC-115 |

"Tier" is the in-memory `IsPlatform` flag of D16 - **not** a database column. A platform-tier
permission can never be granted through a tenant role (AC-041), so in practice only the seeded
platform `Admin` role holds one.

**Why the tier split falls here.** The spec's actor list gives a tenant administrator "view their
tenant, manage its members and their role assignments, and manage the tenant's roles" and says they
"cannot change the tenant's own lifecycle status", while creating, renaming, suspending, reactivating
and deleting tenants are all listed under the platform administrator (AC-002, AC-005, AC-006, AC-008,
AC-009 each name a platform administrator as the actor). So `Tenant_View` and the four
`TenantMember_*` permissions are tenant-tier and belong to every tenant's system-created administrator
role (AC-042); the six remaining constants are platform-tier and are unreachable from inside a tenant
(AC-041, AC-135).

Mirrored one-for-one, same keys and same values, in `src/frontend/web/allow.ts` (`permissions` skill,
step 4). Route gating (`auth-urls.ts`, `nav-items.ts`, `searchable-items.ts`) is specified in
`frontend-contract.md`.

### 2.2 Declaration - `src/backend/Source/Features/Tenancy/Core/TenancyPermissionsProvider.cs`

New provider, discovered by reflection (nothing to register). `GroupName => "Tenancy"` - the section
heading on the *Roles -> Change permissions* screen. Three parent nodes; only leaves become real
permissions, so no `Permissions(...)` ever points at a parent:

| Parent node (`AddPermission`) | Display | Children (`AddChild`) |
| --- | --- | --- |
| `"Tenants"` | `"Tenants"` | `Allow.Tenant_View` "View"; `Allow.Tenant_Create` "Create" *(platform)*; `Allow.Tenant_Update` "Update" *(platform)*; `Allow.Tenant_Suspend` "Suspend" *(platform)*; `Allow.Tenant_Reactivate` "Reactivate" *(platform)*; `Allow.Tenant_Delete` "Delete" *(platform)* |
| `"TenantMembers"` | `"Tenant Members"` | `Allow.TenantMember_View` "View"; `Allow.TenantMember_Add` "Add"; `Allow.TenantMember_UpdateRoles` "UpdateRoles"; `Allow.TenantMember_Remove` "Remove" |
| `"Platform"` | `"Platform"` | `Allow.Platform_Administration` "Administration" *(platform)* |

*(platform)* means the leaf is declared through the `isPlatform: true` overload added by D16.
`IdentityPermissionsProvider` and `FileManagementPermissionsProvider` are unchanged - none of their
permissions is platform-tier.

Supporting catalogue changes (D16, in-memory only - no `Permission` entity change, no `DataSeeder`
change, no migration):

- `PermissionDefinition` and the flattened permission gain an `IsPlatform` flag.
- `PermissionDefinition.AddChild` and `PermissionDefinitionContext.AddPermission` gain a
  `bool isPlatform = false` parameter.
- `IPermissionDefinitionService` gains `GetPlatformPermissionNames()`, read by
  `GetDefinePermissionsEndpoint` (AC-114, AC-115) and `ChangePermissionsEndpoint` (AC-041).

### 2.3 What AC-045 resolves to

A caller lacking the permission an operation requires is refused by the framework's `Permissions(...)`
policy with the standard **403**. No new `ErrorCodes` constant is minted for it: AC-067's exhaustive
list does not name one, and the plan's error workstream enumerates exactly the twelve codes in §3.
The web client's existing `error.403` handling supplies the message.

---

## 3. Error codes

Added to `src/backend/Source/ErrorHandling/ErrorCodes.cs` as camelCase string constants. Each gets an
`error.server.<code>` key in all eight `src/frontend/web/public/locales/*.json` files; those keys are
owned by the single locale task in `frontend-contract.md` (AC-069, AC-075, AC-076).

| Constant | Value | Condition that raises it | Raised by | Status | Field |
| --- | --- | --- | --- | --- | --- |
| `TenantNotFound` | `tenantNotFound` | a request addresses a tenant that does not exist, is soft-deleted, or that the caller may not see; or the session names such a tenant | every `tenants/{id}` and `tenants/{tenantId}/…` endpoint; `TenantSwitchEndpoint`; `TenantContextProcessor` | 400 (403 from the processor) | - |
| `TenantSuspended` | `tenantSuspended` | a tenant-scoped operation is attempted in a suspended tenant; a suspended tenant is switched into; a member is added to one; a suspended tenant's stored file is requested | `TenantContextProcessor`; `TenantSwitchEndpoint`; `TenantMemberAddEndpoint`; `FileGetEndpoint` | 403 from the processor, else 400 | - |
| `NotTenantMember` | `notTenantMember` | the caller holds no active membership in the tenant the request acts in or addresses, and is not a platform administrator | `TenantContextProcessor`; the four `TenantMember*` endpoints; `TenantSwitchEndpoint` | 403 from the processor, else 400 | - |
| `NoActiveTenant` | `noActiveTenant` | an authenticated request reaches a tenant-scoped operation with no active tenant established | `TenantContextProcessor`; `FileUploadEndpoint` for a tenant-scoped upload | 403 from the processor, 400 from the upload | - |
| `TenantIdentifierAlreadyExists` | `tenantIdentifierAlreadyExists` | the normalized identifier is already used by another tenant, **including a soft-deleted one**, compared case-insensitively | `TenantCreateEndpoint`, `TenantUpdateEndpoint`, `TenantOnboardEndpoint` | 400 | `identifier` |
| `DuplicateTenantMembership` | `duplicateTenantMembership` | the named account already holds an **active** membership in the tenant (removed memberships do not count) | `TenantMemberAddEndpoint` | 400 | `userId` |
| `LastTenantAdministrator` | `lastTenantAdministrator` | removing a membership, or replacing its roles, would leave the tenant with no member holding tenant administration | `TenantMemberRemoveEndpoint`, `TenantMemberUpdateRolesEndpoint` | 400 | - |
| `SystemCreatedTenantCannotBeModified` | `systemCreatedTenantCannotBeModified` | a rename, suspend or delete targets the system-created bootstrap tenant | `TenantUpdateEndpoint`, `TenantSuspendEndpoint`, `TenantDeleteEndpoint` | 400 | - |
| `CrossTenantFileAccess` | `crossTenantFileAccess` | a stored file attributed to another tenant, or account-owned by another account, is read or deleted | `FileGetEndpoint`, `FileDeleteEndpoint` | 400 | - |
| `TenantMembershipRevoked` | `tenantMembershipRevoked` | the session's tenant is live but the caller's membership in it has been removed since the session was established | `TenantContextProcessor` | 403 | - |
| `PlatformPermissionNotGrantable` | `platformPermissionNotGrantable` | a permission change would grant a platform-tier permission to a tenant role | `ChangePermissionsEndpoint` | 400 | `permissions` |
| `ConcurrentModification` | `concurrentModification` | the membership row's `xmin` token was stale when the role-assignment replacement was saved | `TenantMemberUpdateRolesEndpoint` | 400 | - |

Criteria served: AC-003, AC-007, AC-010, AC-011, AC-015, AC-019, AC-020, AC-023, AC-026, AC-029,
AC-037, AC-041, AC-050, AC-058, AC-059, AC-060, AC-067, AC-068, AC-070, AC-081, AC-099, AC-106,
AC-107, AC-109, AC-127, AC-134, AC-140, AC-144, AC-147.

**Reused rather than re-minted** (`api-error-handling`: reuse a code that already describes the
situation):

| Existing code | Reused for | Criteria |
| --- | --- | --- |
| `ErrorCodes.UserNotFound` | a membership is requested for an account that does not exist | AC-016 |
| `ErrorCodes.ReferencedRecordNotFound` | a request names a role id that does not belong to the tenant it assigns in | AC-014, AC-017, AC-096 |
| `ErrorCodes.RoleNameAlreadyExists` | a role name already used **within the same tenant**, including deleted roles | AC-039, AC-144, AC-147 |
| `ErrorCodes.SystemCreatedRoleCannotBeDeleted` / `…CannotBeUpdated` / `SystemCreatedRolePermissionsCannotBeChanged` | a tenant's system-created administrator role | AC-043 |
| `ErrorCodes.UserNotActive` | a globally deactivated account attempting to sign in | AC-104 |

Together these cover AC-067 exhaustively: unknown tenant, suspended tenant, not a member, no active
tenant, duplicate identifier, duplicate membership, last administrator, protected system-created
tenant (`SystemCreatedTenantCannotBeModified`) or role (the three existing role codes), and
cross-tenant file access.

**Field attribution (AC-068).** Where a failure is attributable to one input field, the
property-scoped overload is used - `ThrowError(x => x.Identifier, …)`, `ThrowError(x => x.UserId, …)`,
`ThrowError(x => x.Permissions, …)` - so `errors[].name` carries the camelCased request property and
the web side can attach it to the form field. All other failures are request-level (empty `name`).

---

## 4. Tenant-context enforcement and the exemption list

`TenantContextProcessor` (`src/backend/Source/Processors/TenantContextProcessor.cs`, D10) is a global
pre-processor attached in `Program.cs` beside `ToLargePayloadProcessor` in the existing
`c.Endpoints.Configurator`. It is the single enforcement point for AC-007, AC-022, AC-023, AC-026,
AC-029, AC-037, AC-050 and AC-099, so no endpoint repeats the check.

Behaviour, in order, for an **authenticated** request whose endpoint type does **not** carry
`[AllowNoTenant]`:

| Condition | Response |
| --- | --- |
| no tenant established for the session | 403 + `noActiveTenant` |
| the session's tenant does not exist or is soft-deleted | 403 + `tenantNotFound` |
| the session's tenant is suspended | 403 + `tenantSuspended` |
| the caller holds no active membership in it | 403 + `tenantMembershipRevoked` when a membership existed when the session was established, otherwise `notTenantMember` |
| otherwise | the request proceeds with the tenant resolved |

The refusal is emitted through the standard problem-details path so the body carries
`errors[].code` exactly as a `ThrowError` failure does; the caller **stays authenticated** (D9,
AC-020, AC-029, AC-070) - only the password-hash session-version check in
`SessionValidationMiddleware` still signs a user out. Unauthenticated requests are not the
processor's business: they are refused by authentication (401) or allowed through on an
`AllowAnonymous()` endpoint.

`[AllowNoTenant]` (`src/backend/Source/Attributes/AllowNoTenantAttribute.cs`) is applied to exactly
these endpoint types, and to no others:

| Endpoint | Why exempt |
| --- | --- |
| `TokenEndpoint`, `SignupEndpoint`, `VerifyEmailEndpoint`, `ResendVerifyEmailEndpoint`, `ForgetPasswordEndpoint`, `ResetPasswordEndpoint` | anonymous account flows (AC-051, AC-119) |
| `ChangePasswordEndpoint`, `ProfileEndpoint`, `UpdateProfileEndpoint`, `GetInfoEndpoint`, `SignoutEndpoint` | authenticated account self-service, usable with no tenant (AC-051, AC-122, AC-126) |
| the framework refresh-token route `account/refresh-token` | re-issues a session that may carry no tenant (AC-140) |
| all thirteen `tenants/*` endpoints | the tenant they act on is addressed by route id or is being chosen; each performs its own visibility check (see §5.0) |
| `FileUploadEndpoint`, `FileGetEndpoint`, `FileDeleteEndpoint` | account-owned files must work with no tenant (AC-097); these three enforce tenant attribution themselves (AC-058, AC-099) |

Every other endpoint in the application - users, roles, permissions, notifications - requires an
established, existing, unsuspended tenant with a live membership.

---

## 5. New endpoints - Tenancy feature

Feature root `src/backend/Source/Features/Tenancy/` (D2). Area `Endpoints/Tenants/`.

### 5.0 Route group and the shared access rules

**`TenantsGroup`** - `src/backend/Source/Features/Tenancy/Endpoints/Tenants/TenantsGroup.cs`,
`sealed class TenantsGroup : Group`, `Configure("tenants", ep => {})`. It owns the prefix; endpoints
declare only the remainder.

Three rules are shared by the endpoints in this area rather than restated in each:

- **R1 - platform tier test.** "Platform administrator" throughout this document means the caller's
  permission claims contain `Allow.Platform_Administration`. It is a claim test, never a role-name
  test (AC-046, AC-083; and D20's reason: any tenant may define a role called `Admin`).
- **R2 - tenant visibility.** A tenant addressed by `{id}` / `{tenantId}` is visible to a platform
  administrator if it exists and is not soft-deleted; to anyone else only if it exists, is not
  soft-deleted, **and** the caller holds an active membership in it. Not visible -> `tenantNotFound`
  (AC-010, AC-046, AC-066).
- **R3 - membership-surface authorization.** On the four `tenants/{tenantId}/members…` endpoints the
  membership check runs **before** any existence check: a caller who is neither a platform
  administrator nor an active member of `{tenantId}` is refused with `notTenantMember` whether or not
  that tenant exists, so the surface discloses nothing (AC-021 with AC-010).

`{tenantId}` on these routes names *the tenant being administered*, not the tenant the request acts
in. It is not, and must never become, a way to choose the acting tenant: the acting tenant travels in
the session only (AC-138), and each of these endpoints authorizes `{tenantId}` explicitly through R2
and R3.

### 5.1 `TenantListEndpoint` - GET `tenants`

| Aspect | Contract |
| --- | --- |
| File | `Features/Tenancy/Endpoints/Tenants/TenantListEndpoint.cs` |
| Configure | `Get("")`; `Group<TenantsGroup>()`; `Permissions(Allow.Tenant_View)`; `[AllowNoTenant]` |
| Request | `TenantListRequest : ListRequestDto<Guid>` - adds `TenantStatus? Status` |
| Validator | `TenantListValidator` - `Include(new ListRequestDtoValidator<Guid>())`; `RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue)`; sort whitelist below |
| Sortable fields | `Id`, `Name`, `Identifier`, `Status`, `CreatedAt`, `UpdatedAt` (AC-062); anything else -> 400 (AC-063) |
| Paging | inherited: page 1 / size 10, size bounded 1..100, `All` capped at 10,000, `Total` returned (AC-061) |
| Search | `Search` matches `EF.Functions.ILike(x.Name, "%…%")` or `EF.Functions.Like(x.IdentifierNormalized, "%…%")` with the search term trimmed and lower-cased (AC-064, AC-102) |
| Filter | `Status` filters by lifecycle status (AC-065) |
| Rows | platform administrator (R1) -> every non-deleted tenant (AC-046); otherwise only tenants in which the caller holds an active membership (AC-066). The membership sub-query is written through `AcrossAllTenants()` because memberships are tenant-scoped and the caller may have no active tenant |
| Response | `TenantListResponse : ListDto<TenantListDto>` |
| Row DTO | `TenantListDto : AuditableDto<Guid>` - `bool SystemCreated`, `string Name`, `string Identifier`, `string IdentifierNormalized`, `TenantStatus Status` |
| Mapper | `TenantListDtoMapper` - `[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]`, `public partial TenantListDto Map(Tenant entity)` |
| Errors | none beyond validation and 403 |
| Criteria | AC-046, AC-061, AC-062, AC-063, AC-064, AC-065, AC-066, AC-071 |

No member count is exposed on the row: counting memberships would have to opt out of the tenant
filter per row, and `TenantMemberListEndpoint` already returns `Total` for the tenant being viewed.

### 5.2 `TenantGetEndpoint` - GET `tenants/{id}`

| Aspect | Contract |
| --- | --- |
| Configure | `Get("{id}")`; `Group<TenantsGroup>()`; `Permissions(Allow.Tenant_View)`; `[AllowNoTenant]` |
| Request | `TenantGetRequest : BaseDto<Guid>` |
| Validator | `TenantGetValidator` - `RuleFor(x => x.Id).NotEmpty()` |
| Response | `TenantGetResponse : AuditableDto<Guid>` - `bool SystemCreated`, `string Name`, `string Identifier`, `string IdentifierNormalized`, `TenantStatus Status` |
| Mapper | `TenantGetResponseMapper` - `[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]`, `Map(Tenant entity)` |
| Errors | R2 fails -> `tenantNotFound` (400) |
| Criteria | AC-010, AC-012, AC-046, AC-066, AC-071, AC-078 |

Audit fields (`CreatedBy`/`CreatedAt`/`UpdatedBy`/`UpdatedAt`) come from `AuditableDto<Guid>` and are
what makes AC-012 and AC-078 observable over the API.

### 5.3 `TenantCreateEndpoint` - POST `tenants`

| Aspect | Contract |
| --- | --- |
| Configure | `Post("")`; `Group<TenantsGroup>()`; `Permissions(Allow.Tenant_Create)`; `[AllowNoTenant]` |
| Request | `TenantCreateRequest` - `string Name`, `string Identifier` |
| Validator | `TenantCreateValidator` - `RuleFor(x => x.Name).NotEmpty().TenantName()`; `RuleFor(x => x.Identifier).NotEmpty().TenantIdentifier()` (§5.14) |
| Guards | trimmed, lower-cased identifier already used by any tenant **including soft-deleted ones** -> `ThrowError(x => x.Identifier, …, ErrorCodes.TenantIdentifierAlreadyExists)`. The comparison reads `IdentifierNormalized` with the soft-delete filter suppressed by name (`IgnoreQueryFilters(["SoftDelete"])`); the composite unique index is the backstop (AC-003, AC-144) |
| Effect | persists the tenant `Status = TenantStatus.Active`, `SystemCreated = false`, `CreatedBy`/`CreatedAt` stamped centrally; provisions the tenant's system-created administrator role holding every tenant-tier permission (§7 method 2, AC-042). No membership is created - a platform-created tenant has no first member yet (see §5.9) |
| Response | `TenantCreateResponse : BaseDto<Guid>` - `bool SystemCreated`, `string Name`, `string Identifier`, `string IdentifierNormalized`, `TenantStatus Status` |
| Mappers | `TenantCreateRequestMapper` - `[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Source)]`, `Map(TenantCreateRequest request) -> Tenant`; `TenantCreateResponseMapper` - `Target`, `Map(Tenant entity)` |
| Errors | `tenantIdentifierAlreadyExists` (400, field `identifier`); validation failures (400, field-level, AC-004) |
| Criteria | AC-001, AC-002, AC-003, AC-004, AC-012, AC-042, AC-071, AC-100, AC-101, AC-102, AC-144 |

### 5.4 `TenantUpdateEndpoint` - PUT `tenants/{id}`

| Aspect | Contract |
| --- | --- |
| Configure | `Put("{id}")`; `Group<TenantsGroup>()`; `Permissions(Allow.Tenant_Update)`; `[AllowNoTenant]` |
| Request | `TenantUpdateRequest : BaseDto<Guid>` - `string Name`, `string Identifier` |
| Validator | `TenantUpdateValidator` - `RuleFor(x => x.Id).NotEmpty()`; the same `TenantName()` / `TenantIdentifier()` rules |
| Guards, in order | R2 fails -> `tenantNotFound`; `entity.SystemCreated` -> `systemCreatedTenantCannotBeModified` (AC-011); identifier used by a **different** tenant including soft-deleted ones -> `ThrowError(x => x.Identifier, …, TenantIdentifierAlreadyExists)` (AC-003) |
| Effect | persists name and identifier; `UpdatedBy`/`UpdatedAt` stamped centrally (AC-005, AC-012, AC-078) |
| Response | `TenantUpdateResponse : BaseDto<Guid>` - same members as the create response |
| Mappers | `TenantUpdateRequestMapper` - `Source`, `void Update(TenantUpdateRequest request, Tenant entity)`; `TenantUpdateResponseMapper` - `Target`, `Map(Tenant entity)` |
| Criteria | AC-003, AC-004, AC-005, AC-010, AC-011, AC-012, AC-071, AC-078, AC-100, AC-101, AC-102, AC-144 |

### 5.5 `TenantSuspendEndpoint` - POST `tenants/{id}/suspend`

| Aspect | Contract |
| --- | --- |
| Configure | `Post("{id}/suspend")`; `Group<TenantsGroup>()`; `Permissions(Allow.Tenant_Suspend)`; `[AllowNoTenant]` |
| Request | `TenantSuspendRequest : BaseDto<Guid>` |
| Validator | `TenantSuspendValidator` - `RuleFor(x => x.Id).NotEmpty()` |
| Guards | R2 fails -> `tenantNotFound`; `entity.SystemCreated` -> `systemCreatedTenantCannotBeModified` (AC-011). Suspending an already-suspended tenant succeeds unchanged (idempotent - no criterion defines a failure for it) |
| Effect | `Status = TenantStatus.Suspended`; **no** data is deleted or altered (AC-006). Members' next requests are refused by `TenantContextProcessor` with `tenantSuspended` (AC-007) with no sign-out (AC-020, AC-029, AC-070) |
| Response | `TenantSuspendResponse : BaseDto<Guid>` - `TenantStatus Status` |
| Mapper | `TenantSuspendResponseMapper` - `Target`, `Map(Tenant entity)` |
| Criteria | AC-006, AC-007, AC-010, AC-011, AC-020, AC-029, AC-071 |

Two endpoints, not one `TenantSetStatusEndpoint`, because AC-071 requires each operation to be gated
by its own permission and a branch inside a handler is invisible to `Permissions(...)` and to the web
client's `isAllowed` check (D15).

### 5.6 `TenantReactivateEndpoint` - POST `tenants/{id}/reactivate`

Identical in shape to §5.5 with `Post("{id}/reactivate")`, `Permissions(Allow.Tenant_Reactivate)`,
`TenantReactivateRequest`, `TenantReactivateValidator`, `TenantReactivateResponse : BaseDto<Guid>`
(`TenantStatus Status`) and `TenantReactivateResponseMapper`. Sets `Status = TenantStatus.Active`;
members regain access on their next request with no action of their own (AC-008). The bootstrap
tenant cannot be suspended, so reactivating it is a no-op rather than an error - AC-011 names rename,
suspend and delete only. Criteria: AC-008, AC-010, AC-071.

### 5.7 `TenantDeleteEndpoint` - DELETE `tenants/{id}`

| Aspect | Contract |
| --- | --- |
| Configure | `Delete("{id}")`; `Group<TenantsGroup>()`; `Permissions(Allow.Tenant_Delete)`; `[AllowNoTenant]` |
| Request | `TenantDeleteRequest : BaseDto<Guid>` |
| Validator | `TenantDeleteValidator` - `RuleFor(x => x.Id).NotEmpty()` |
| Guards | R2 fails -> `tenantNotFound`; `entity.SystemCreated` -> `systemCreatedTenantCannotBeModified` (AC-011) |
| Effect | soft delete (the row is retained, excluded from every query by the `SoftDelete` filter); the tenant's data becomes unreachable through the tenant filter (AC-009, AC-079). Members are refused with `tenantNotFound` on their next request and stay signed in (AC-029) |
| Response | `TenantDeleteResponse` - `bool Success`, `string Message` (mirrors `RoleDeleteResponse`) |
| Mapper | none - the response is constructed inline, as `RoleDeleteEndpoint` does |
| Criteria | AC-009, AC-010, AC-011, AC-029, AC-071, AC-079 |

### 5.8 `TenantMemberListEndpoint` - GET `tenants/{tenantId}/members`

| Aspect | Contract |
| --- | --- |
| Configure | `Get("{tenantId}/members")`; `Group<TenantsGroup>()`; `Permissions(Allow.TenantMember_View)`; `[AllowNoTenant]` |
| Request | `TenantMemberListRequest : ListRequestDto<Guid>` - `Guid TenantId` (route), `Guid? RoleId` |
| Validator | `TenantMemberListValidator` - `Include(new ListRequestDtoValidator<Guid>())`; `RuleFor(x => x.TenantId).NotEmpty()`; sort whitelist below |
| Sortable fields | `Id`, `Username`, `Email`, `FirstName`, `LastName`, `IsActive`, `CreatedAt`, `UpdatedAt` - the same whitelist `UserListEndpoint` uses, because the page is produced over user accounts holding an active membership (AC-062, AC-063) |
| Paging | inherited: page 1 / size 10, bounded 1..100, `All` capped at 10,000, `Total` returned (AC-061) |
| Search | over the member's username and email, matched on `UsernameNormalized` / `EmailNormalized` (AC-064) |
| Filter | `RoleId` limits to members holding that role **in this tenant** |
| Access | R3; platform administrators see any tenant's members (AC-046) |
| Rows | accounts holding an active membership in `{tenantId}`, with the roles they hold **in that tenant** only (AC-013, AC-027) |
| Response | `TenantMemberListResponse : ListDto<TenantMemberListDto>` |
| Row DTOs | `TenantMemberListDto : AuditableDto<Guid>` (`Id` is the **user account id**) - `string Username`, `string Email`, `string? FirstName`, `string? LastName`, `bool IsActive`, `DateTime MemberSince`, `List<TenantMemberRoleDto> Roles`; `TenantMemberRoleDto : BaseDto<Guid>` - `string Name` |
| Mapper | `TenantMemberListDtoMapper` - `Target`, `Map(TenantMemberDto item)` mapping the §7 service DTO onto the row DTO |
| Errors | R3 fails -> `notTenantMember` (400) |
| Criteria | AC-013, AC-021, AC-046, AC-061, AC-062, AC-063, AC-064, AC-072 |

The rows come from `ITenantAuthorizationService.GetTenantMembersAsync` (§7 method 5) rather than a
local query, because the row data is user-account data owned by `Features/Identity` and
`FeatureDependencyTests` forbids `Features/Tenancy` from touching `User`.

### 5.9 `TenantMemberAddEndpoint` - POST `tenants/{tenantId}/members`

| Aspect | Contract |
| --- | --- |
| Configure | `Post("{tenantId}/members")`; `Group<TenantsGroup>()`; `Permissions(Allow.TenantMember_Add)`; `[AllowNoTenant]` |
| Request | `TenantMemberAddRequest` - `Guid TenantId` (route), `Guid UserId`, `List<Guid> Roles = []` |
| Validator | `TenantMemberAddValidator` - `RuleFor(x => x.TenantId).NotEmpty()`; `RuleFor(x => x.UserId).NotEmpty()`; `RuleFor(x => x.Roles).NotNull()`; `RuleForEach(x => x.Roles).NotEmpty()` |
| Guards, in order | R3 fails -> `notTenantMember`; tenant not visible (platform administrator case) -> `tenantNotFound`; tenant suspended -> `tenantSuspended` (AC-007); account does not exist -> `ErrorCodes.UserNotFound` attributed to `userId` (AC-016); an **active** membership already exists -> `duplicateTenantMembership` attributed to `userId` (AC-015, AC-107 - a previously removed membership does not block, AC-106); any role id does not belong to `{tenantId}` -> `ErrorCodes.ReferencedRecordNotFound` attributed to `roles` (AC-014) |
| Effect | creates the membership row and grants exactly the roles named (AC-014). **If the tenant currently has no active member**, the tenant's system-created administrator role is added to that set, so AC-042's "assign that role to the tenant's first member" holds for a tenant a platform administrator created empty, and AC-019 can never be violated at creation time. Other memberships of the same account are untouched (AC-013) |
| Response | `TenantMemberAddResponse : BaseDto<Guid>` (`Id` = membership id) - `Guid TenantId`, `Guid UserId`, `List<Guid> Roles` |
| Mapper | none - constructed inline, following the `ChangePermissionsEndpoint` precedent, because `Roles` is not a property of the membership entity |
| Criteria | AC-013, AC-014, AC-015, AC-016, AC-042, AC-072, AC-078, AC-106, AC-107 |

### 5.10 `TenantMemberUpdateRolesEndpoint` - PUT `tenants/{tenantId}/members/{userId}/roles`

| Aspect | Contract |
| --- | --- |
| Configure | `Put("{tenantId}/members/{userId}/roles")`; `Group<TenantsGroup>()`; `Permissions(Allow.TenantMember_UpdateRoles)`; `[AllowNoTenant]` |
| Request | `TenantMemberUpdateRolesRequest` - `Guid TenantId`, `Guid UserId` (both route), `List<Guid> Roles = []` |
| Validator | `TenantMemberUpdateRolesValidator` - `TenantId`/`UserId` `NotEmpty()`; `RuleFor(x => x.Roles).NotNull()`; `RuleForEach(x => x.Roles).NotEmpty()` |
| Guards, in order | R3 fails -> `notTenantMember`; tenant not visible -> `tenantNotFound`; tenant suspended -> `tenantSuspended`; no active membership for `{userId}` -> `Send.NotFoundAsync` (404 - a membership is a tenant-scoped record, §1.1 rule two); a role id outside the tenant -> `ErrorCodes.ReferencedRecordNotFound` on `roles`; the change would leave the tenant with no member holding tenant administration -> `lastTenantAdministrator` (AC-019) |
| Effect | **replaces** the member's role assignments for this tenant with exactly the set supplied, inside one transaction, touching the membership row so its `xmin` token advances (AC-017, AC-081, D14). The member's assignments in other tenants are untouched (AC-013). The change reaches the member's live sessions on their next request, with no re-sign-in (AC-116) |
| Concurrency | a stale `xmin` surfaces as `DbUpdateConcurrencyException` and is reported as `concurrentModification` (400), so a losing concurrent writer fails visibly instead of overwriting a set it never saw (AC-081) |
| Response | `TenantMemberUpdateRolesResponse : BaseDto<Guid>` (`Id` = membership id) - `Guid TenantId`, `Guid UserId`, `List<Guid> Roles` |
| Mapper | none - constructed inline |
| Criteria | AC-013, AC-017, AC-019, AC-072, AC-078, AC-081, AC-116 |

### 5.11 `TenantMemberRemoveEndpoint` - DELETE `tenants/{tenantId}/members/{userId}`

| Aspect | Contract |
| --- | --- |
| Configure | `Delete("{tenantId}/members/{userId}")`; `Group<TenantsGroup>()`; `Permissions(Allow.TenantMember_Remove)`; `[AllowNoTenant]` |
| Request | `TenantMemberRemoveRequest` - `Guid TenantId`, `Guid UserId` (both route) |
| Validator | `TenantMemberRemoveValidator` - both `NotEmpty()` |
| Guards, in order | R3 fails -> `notTenantMember`; tenant not visible -> `tenantNotFound`; no active membership -> `Send.NotFoundAsync` (404); removal would leave the tenant with no member holding tenant administration -> `lastTenantAdministrator` (AC-019) |
| Effect | soft-deletes the membership and drops the member's role assignments for that tenant's roles, inside one transaction. The user account, its other memberships and its other tenants' access are untouched (AC-018, AC-013, AC-079). Their live sessions lose access to this tenant at their next request, with no password change and no sign-out (AC-020) |
| Response | `TenantMemberRemoveResponse` - `bool Success`, `string Message` |
| Mapper | none |
| Criteria | AC-013, AC-018, AC-019, AC-020, AC-072, AC-079 |

### 5.12 `TenantOnboardEndpoint` - POST `tenants/onboard`

Self-service tenant creation by an account that is already authenticated (AC-133..AC-137).

| Aspect | Contract |
| --- | --- |
| Configure | `Post("onboard")`; `Group<TenantsGroup>()`; **no** `Permissions(...)` and **no** `AllowAnonymous()` - authentication alone is the gate; `[AllowNoTenant]` |
| Request | `TenantOnboardRequest` - `string Name`, `string Identifier` |
| Validator | `TenantOnboardValidator` - the identical `TenantName()` / `TenantIdentifier()` rules used by create and update (AC-134) |
| Guards | unauthenticated -> **401**, which is the "defined error code" AC-136 asks for at the transport level (the endpoint is simply not anonymous); duplicate normalized identifier including soft-deleted tenants -> `ThrowError(x => x.Identifier, …, TenantIdentifierAlreadyExists)` (AC-134) |
| Effect | creates the tenant `Active` with `SystemCreated = false` and the caller recorded as `CreatedBy` (AC-137); provisions its system-created administrator role (AC-042); creates the caller's membership and assigns them that role (AC-133); grants **no** platform-tier permission (AC-135, AC-120); permitted no matter how many memberships the caller already holds (AC-137); re-establishes the session with the new tenant active so the caller is not asked to sign in again (AC-133, AC-139) - through the same path as §5.13 |
| Response | `TenantOnboardResponse : BaseDto<Guid>` (`Id` = new tenant id) - `string Name`, `string Identifier`, `string IdentifierNormalized`, `TenantStatus Status`, `TenantSessionDto Session` |
| Mappers | `TenantOnboardResponseMapper` - `Target`, `Map(Tenant entity)`; `Session` is assigned from the §7 service result after mapping |
| Criteria | AC-042, AC-120, AC-133, AC-134, AC-135, AC-136, AC-137, AC-139, AC-143 |

The tenant-creation work is the *same* service path `TenantCreateEndpoint` uses, so the naming rules,
the duplicate comparison and the error codes cannot diverge between the two (AC-134).

### 5.13 `TenantSwitchEndpoint` - POST `tenants/switch`

| Aspect | Contract |
| --- | --- |
| Configure | `Post("switch")`; `Group<TenantsGroup>()`; **no** `Permissions(...)` - any authenticated caller may switch into a tenant they belong to; `[AllowNoTenant]` (this is the endpoint that establishes a tenant) |
| Request | `TenantSwitchRequest` - `Guid TenantId` |
| Validator | `TenantSwitchValidator` - `RuleFor(x => x.TenantId).NotEmpty()` |
| Guards, in order | tenant absent or soft-deleted -> `tenantNotFound` (AC-010, AC-127); caller holds no active membership -> `notTenantMember`, **whatever permissions they hold in another tenant** (AC-026, AC-109); tenant suspended -> `tenantSuspended` (AC-007, AC-029) |
| Effect | re-establishes the session carrying the newly selected tenant while leaving the caller authenticated and re-entering no credentials (AC-139): the cookie principal is re-issued, a fresh access/refresh token pair is issued, the `refreshToken` cookie is rewritten exactly as `TokenEndpoint` writes it, and the refresh-token row records the tenant so a later refresh cannot resurrect the previous tenant's authority (AC-108, AC-109). Roles and permissions are recomputed from the membership and assignments **as they stand at request time** (AC-108, AC-027) |
| Response | `TenantSwitchResponse` - `Guid TenantId`, `string Name`, `string Identifier`, `TenantStatus Status`, `TenantSessionDto Session` |
| Mapper | none - constructed inline from the tenant entity and the §7 service result |
| Criteria | AC-007, AC-010, AC-025, AC-026, AC-027, AC-029, AC-108, AC-109, AC-127, AC-139, AC-140, AC-149 |

`TenantSessionDto` (declared in the root `Backend.Tenancy` namespace so both this feature and
`Features/Identity` may name it without a cross-feature reference) carries exactly what the existing
sign-in response carries: `Guid UserId`, `string AccessToken`, `DateTime AccessTokenExpiry`,
`string RefreshToken`, `DateTime RefreshTokenExpiry`. Cookie-authenticated clients ignore it; JWT
clients replace their pair with it.

### 5.14 Shared validation rules - `Features/Tenancy/Core/TenantValidationRules.cs`

Two FluentValidation rule-builder extensions, used by `TenantCreateValidator`,
`TenantUpdateValidator` and `TenantOnboardValidator` so all three enforce one rule set (AC-134):

| Extension | Rule | Criteria |
| --- | --- | --- |
| `TenantName()` | the value, **trimmed**, is 2..100 characters | AC-100, AC-004 |
| `TenantIdentifier()` | the value, **trimmed**, is 3..50 characters and matches `^[a-z0-9]+(?:-[a-z0-9]+)*$` - lower-case ASCII letters, digits and hyphens, beginning and ending with a letter or digit, never two consecutive hyphens | AC-101, AC-004 |

Both failures are field-level, so `errors[].name` is `name` / `identifier` (AC-004, AC-068). The web
forms mirror the same bounds and pattern (`frontend-contract.md`), so a rejection is not the first
place a user learns the rule (spec, "Naming and normalization").

Uniqueness is compared on the normalized lower-case column, never on the entered value (AC-102), and
the comparison includes soft-deleted tenants (AC-003, AC-144, AC-147).

---

## 6. Changed existing endpoints

Routes, permissions and payload shapes are unchanged except where a row below says otherwise. The
criteria describe row filters, not new surfaces (AC-093..AC-096, AC-110..AC-113).

### 6.1 Identity - Account (`AccountGroup`, prefix `account`)

| Endpoint | Change | Criteria |
| --- | --- | --- |
| `TokenEndpoint` POST `token` | after authentication, resolve the caller's active memberships: **exactly one** -> the tenant claim is issued for it and the session starts inside it; **zero or more than one** -> **no** tenant claim is issued and every tenant-scoped operation is refused with `noActiveTenant` until the caller selects one. Adds `[AllowNoTenant]`. Response shape unchanged | AC-024, AC-049, AC-050, AC-104, AC-123, AC-140, AC-142 |
| `GetInfoEndpoint` GET `get-info` | `UserGetInfoResponse` gains `Guid? ActiveTenantId`, `TenantInfoDto? ActiveTenant`, `List<TenantInfoDto> Tenants` (every tenant in which the caller holds an active membership) and `bool IsPlatformAdministrator`. `Roles`/`Permissions` are those of the **active tenant only**; with no active tenant they are the caller's platform-tier roles, which is an empty list for an ordinary account. New nested DTO `TenantInfoDto : BaseDto<Guid>` - `string Name`, `string Identifier`, `TenantStatus Status`. Adds `[AllowNoTenant]` | AC-024, AC-027, AC-050, AC-073, AC-108, AC-122, AC-123, AC-127, AC-142 |
| `SignupEndpoint` POST `signup` | stops looking up and assigning the `Public` role; the new account is created with no role, no membership and no platform permission. Adds `[AllowNoTenant]`. Request and response shapes unchanged | AC-118, AC-119, AC-120, AC-121, AC-122, AC-145 |
| `SignoutEndpoint` POST `signout` | adds `[AllowNoTenant]` (unchanged otherwise; discarding the client-side tenant selection is the web app's part of AC-125) | AC-051, AC-125 |
| `ChangePasswordEndpoint`, `ProfileEndpoint`, `UpdateProfileEndpoint` | add `[AllowNoTenant]` so account self-service, including viewing and setting a profile image, works with no active tenant | AC-051, AC-097, AC-122 |
| `ForgetPasswordEndpoint`, `ResetPasswordEndpoint`, `VerifyEmailEndpoint`, `ResendVerifyEmailEndpoint` | add `[AllowNoTenant]` | AC-051, AC-119 |
| `TokenService` (the framework `account/refresh-token` route) | rebuilds the principal from the refresh-token row, including the tenant it was issued for, and recomputes roles and permissions for that tenant; a refresh can neither drop the tenant nor carry the previous one | AC-108, AC-109, AC-124 |

### 6.2 Identity - Users (`UsersGroup`, prefix `users`)

All five keep their routes, permissions, request and response shapes; only the rows change.

| Endpoint | Change | Criteria |
| --- | --- | --- |
| `UserListEndpoint` GET `` | acting in a tenant, lists only accounts holding an active membership in it; a platform administrator lists every account. Sort whitelist and search unchanged | AC-093, AC-095 |
| `UserGetEndpoint` GET `{id}` | an account with no membership in the active tenant answers exactly as a missing account - 404 - unless the caller is a platform administrator | AC-094, AC-095 |
| `UserUpdateEndpoint` PUT `{id}` | same visibility rule; roles assignable are those of the active tenant, others -> `ErrorCodes.ReferencedRecordNotFound` | AC-094, AC-095 |
| `UserDeleteEndpoint` DELETE `{id}` | same visibility rule | AC-094, AC-095 |
| `UserCreateEndpoint` POST `` | additionally creates a membership for the new account in the active tenant, so a tenant administrator never creates an account they cannot then see; `Roles` must name roles of the active tenant, others -> `ErrorCodes.ReferencedRecordNotFound` | AC-096, AC-030 |

### 6.3 Identity - Roles (`RolesGroup`, prefix `roles`) and Permissions (`PermissionsGroup`, prefix `permissions`)

| Endpoint | Change | Criteria |
| --- | --- | --- |
| `RoleListEndpoint` GET `` | rows restricted to the active tenant's roles; a platform administrator sees roles across tenants. Sort whitelist and search unchanged | AC-110, AC-113 |
| `RoleGetEndpoint` GET `{id}` | a role of another tenant answers exactly as a missing role - 404 | AC-111 |
| `RoleCreateEndpoint` POST `` | the role is attributed to the active tenant without the caller supplying it; the duplicate-name check becomes per-tenant and includes deleted roles, reported as today with `ErrorCodes.RoleNameAlreadyExists` on `name` | AC-039, AC-112, AC-144, AC-147 |
| `RoleUpdateEndpoint` PUT `{id}` | per-tenant duplicate check including deleted roles; a role of another tenant -> 404 | AC-039, AC-111, AC-144 |
| `RoleDeleteEndpoint` DELETE `{id}` | a role of another tenant -> 404; deletion becomes a soft delete, so the name stays reserved | AC-111, AC-144, AC-147 |
| `ChangePermissionsEndpoint` PUT `change-permissions/{id}` | a role of another tenant -> 404; granting a **platform-tier** permission to a tenant role -> `platformPermissionNotGrantable` (400) attributed to `permissions`; the system-created tenant administrator role keeps refusing changes with the existing `systemCreatedRolePermissionsCannotBeChanged` | AC-041, AC-043, AC-111 |
| `GetDefinePermissionsEndpoint` GET `define` | a caller without `Allow.Platform_Administration` receives only tenant-tier definitions; a platform administrator receives the whole catalogue with `IsPlatform` set on each definition so the two tiers are distinguishable | AC-114, AC-115 |
| `GetPermissionsEndpoint` GET `get` | unchanged - it returns stored permission rows, and the catalogue itself stays global (AC-040) | AC-040 |

### 6.4 Notifications (`NotificationsGroup`, prefix `notifications`)

No route, permission or payload change; every endpoint here requires an active tenant (no
`[AllowNoTenant]`).

| Endpoint | Change | Criteria |
| --- | --- | --- |
| `NotificationListEndpoint` GET `` | rows are the active tenant's notifications addressed to the caller or to the whole tenant, **unioned** with platform-wide notifications (`TenantId == null`), reached through a narrowed `AcrossAllTenants()` query | AC-052, AC-053, AC-054 |
| `NotificationGetUnreadCountEndpoint` GET `unread-count` | counts exactly the set the list endpoint would show in the active tenant | AC-056 |
| `NotificationMarkAllAsReadEndpoint` POST `mark-all-as-read` | both raw statements - the `ExecuteUpdateAsync` over user-targeted rows and the `ON CONFLICT` insert into `NotificationVisits` - carry a hand-written tenant predicate, because no query filter reaches them; notifications in the caller's other tenants stay unread | AC-033, AC-055, AC-130 |
| `NotificationGetEndpoint`, `NotificationDeleteEndpoint`, `NotificationMarkAsReadEndpoint`, `NotificationMarkAsUnreadEndpoint` | unchanged in code; a notification of another tenant answers as a missing row (404) through the filter | AC-032 |

### 6.5 FileManagement (`FileGroup`, prefix `file-management`)

| Endpoint | Change | Criteria |
| --- | --- | --- |
| `FileUploadEndpoint` POST `upload` | **drops `AllowAnonymous` behaviour** - authentication is now required (it declares only `AllowFileUploads()`, so it is authenticated by default), and unauthenticated callers get 401. `FileUploadRequest` gains `bool AccountOwned` (default `false`). `AccountOwned = false` -> the file is attributed to the active tenant; if none is established -> `noActiveTenant` (400) and **nothing is stored**. `AccountOwned = true` -> the file is attributed to the caller's account and to no tenant, and works with no active tenant (profile images). `[AllowNoTenant]`, because the endpoint enforces the tenant rule itself. Response shape unchanged | AC-057, AC-097, AC-098, AC-099 |
| `FileGetEndpoint` GET `{fileName}` | authentication required (401 otherwise); `[AllowNoTenant]`. Resolution order: no stored-file record -> 404; account-owned and owned by the caller -> served; account-owned by another account -> `crossTenantFileAccess` (400); tenant-scoped and attributed to the active tenant whose status is active -> served; attributed to a suspended or deleted tenant -> `tenantSuspended` / `tenantNotFound` (400) and not served; attributed to any other tenant -> `crossTenantFileAccess` (400). A guessed stored file name therefore never yields another tenant's content | AC-058, AC-059, AC-060, AC-097, AC-098 |
| `FileDeleteEndpoint` DELETE `{fileName}` | `Permissions(Allow.File_Delete)` unchanged; `[AllowNoTenant]`; the same attribution resolution, with `crossTenantFileAccess` for a file of another tenant or another account | AC-058, AC-098 |

"Replacing" a file in AC-058 is delete + upload; no replace route exists and none is added.
`IStorageProvider`, `LocalStorageProvider.GetSafePath` and the stored-name contract behind
`User.Image` are untouched (D17).

### 6.6 Non-endpoint HTTP surface

| Surface | Change | Criteria |
| --- | --- | --- |
| Hangfire dashboard (`HangfireAuthorizationFilter`) | authorizes on the `Allow.Platform_Administration` permission claim instead of `IsInRole("Admin")`, because once role names are per-tenant any tenant could define a role named `Admin` | AC-047, AC-041 |

---

## 7. Cross-feature service surface these endpoints require

`Features/Tenancy` may not name a type under `Features/Identity` (and vice versa) unless the target
carries `[AllowOutside]`; `FeatureDependencyTests` fails the build otherwise. D3 fixes the shape:
**one** contract, **one** direction (`Tenancy -> Identity`), signatures of `Guid`, `string` and its
own DTOs only - never `User`, `Role` or `UserRole`.

`ITenantAuthorizationService` / `TenantAuthorizationService` -
`src/backend/Source/Features/Identity/Core/TenantAuthorizationService.cs`, interface `[AllowOutside]`,
implementation `[NoDirectUse]`, registered by `IdentityFeature.AddServices`. The endpoints above
require these members:

| # | Member | Used by | Criteria |
| --- | --- | --- | --- |
| 1 | `Task<bool> UserExistsAsync(Guid userId, CancellationToken ct)` | §5.9 | AC-016 |
| 2 | `Task<Guid> ProvisionTenantAdministratorRoleAsync(Guid tenantId, CancellationToken ct)` - creates the tenant's system-created role holding every **tenant-tier** permission | §5.3, §5.12 | AC-042, AC-041 |
| 3 | `Task ReplaceTenantRoleAssignmentsAsync(Guid tenantId, Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken ct)` - replaces the whole set for that tenant, inside the caller's transaction | §5.9, §5.10, §5.11 | AC-014, AC-017, AC-081 |
| 4 | `Task<bool> AnyOtherMemberHoldsTenantAdministrationAsync(Guid tenantId, Guid excludedUserId, CancellationToken ct)` | §5.10, §5.11 | AC-019 |
| 5 | `Task<TenantMemberPageDto> GetTenantMembersAsync(Guid tenantId, ListRequestDto<Guid> request, Guid? roleId, CancellationToken ct)` - paged, sorted, searched over accounts holding an active membership, returning each member's roles **in that tenant** | §5.8 | AC-013, AC-061..AC-064, AC-072 |
| 6 | `Task<bool> AllRolesBelongToTenantAsync(Guid tenantId, IReadOnlyCollection<Guid> roleIds, CancellationToken ct)` | §5.9, §5.10 | AC-014, AC-017 |
| 7 | `Task<TenantSessionDto> ReissueSessionAsync(Guid userId, Guid? tenantId, CancellationToken ct)` - recomputes roles and permissions for the tenant at request time, re-signs the cookie principal, issues a fresh access/refresh pair, records the tenant on the refresh-token row and rewrites the `refreshToken` cookie exactly as `TokenEndpoint` does | §5.12, §5.13 | AC-025, AC-108, AC-109, AC-133, AC-139 |

Members 5-7 are additions to the four responsibilities D3 enumerates. They are required, not
optional: `TenantMemberListEndpoint` pages over user accounts, and `TenantSwitchEndpoint` /
`TenantOnboardEndpoint` must issue a token pair through `Features/Identity`'s refresh-token service -
both are Identity-owned types that `Features/Tenancy` may not name. Keeping them on the one existing
contract preserves D3's "one mark, one direction" rather than adding a second `[AllowOutside]`.

The DTOs this contract exchanges - `TenantSessionDto`, `TenantMemberDto`, `TenantMemberPageDto` -
are declared in the **root** `Backend.Tenancy` namespace (D1's kernel), not inside either feature, so
they cost no further `[AllowOutside]` marks. `ListRequestDto<Guid>` is already root
(`Backend.Base.Dto`).

- `TenantMemberDto` - `Guid UserId`, `string Username`, `string Email`, `string? FirstName`,
  `string? LastName`, `bool IsActive`, `DateTime MemberSince`, `DateTime CreatedAt`,
  `Guid? CreatedBy`, `DateTime? UpdatedAt`, `Guid? UpdatedBy`,
  `List<TenantMemberRoleInfo> Roles` (`Guid Id`, `string Name`).
- `TenantMemberPageDto` - `List<TenantMemberDto> Items`, `int Total`.
- `TenantSessionDto` - as §5.13.

Services owned by the Tenancy slice itself (`ITenantService`, `ITenantMembershipService`,
`TenancyFeature`) are specified in `data-model.md`; the endpoints above consume them directly, with
no marks needed, because they are same-feature types.

---

## 8. Criterion coverage from this contract

Criteria this document is wholly or partly responsible for:

AC-001..AC-012 (§5.1-§5.7), AC-013..AC-021 (§5.8-§5.11), AC-022, AC-023, AC-026, AC-027, AC-029 (§4,
§5.13, §6.1), AC-030 (§6.2), AC-032 (§1.1), AC-039, AC-040, AC-041..AC-047 (§2, §6.3, §6.6),
AC-049..AC-051 (§6.1), AC-052..AC-056 (§6.4), AC-057..AC-060 (§6.5), AC-061..AC-066 (§5.1, §5.8),
AC-067, AC-068 (§3), AC-071, AC-072 (§5), AC-078, AC-079, AC-081 (§5.4, §5.7, §5.10, §5.11),
AC-093..AC-099 (§6.2, §6.5), AC-100..AC-102 (§5.14), AC-104, AC-106..AC-109 (§5.9, §5.13, §6.1),
AC-110..AC-117 (§6.3), AC-118..AC-123 (§6.1), AC-127 (§5.13, §6.1), AC-133..AC-137 (§5.12),
AC-139, AC-140, AC-142, AC-143 (§5.12, §5.13, §6.1), AC-144, AC-147 (§3, §5.3, §6.3), AC-149 (§5.13).

Criteria owned elsewhere and only referenced here: AC-024, AC-025, AC-028, AC-031, AC-033..AC-038,
AC-048, AC-069, AC-070, AC-073..AC-077, AC-080, AC-082..AC-092, AC-103, AC-105, AC-124..AC-126,
AC-128..AC-132, AC-138, AC-141, AC-145, AC-146, AC-148.

---

## 9. Notes for the other contracts

- **`data-model.md`** must supply: `TenantStatus` (Active, Suspended) in `Backend.Data.Entities`;
  `Tenant.IdentifierNormalized` as the only column any uniqueness, lookup or search compares
  (AC-102); a soft-delete filter registered **by the name `SoftDelete`** so §5.3's duplicate check can
  suppress it by name; `ITenantService` and `ITenantMembershipService`; and the `Backend.Tenancy`
  DTOs of §7. Index database names must end in the segment the web form calls the field
  (`IX_Tenants_Identifier` -> `identifier`), because `ExceptionProcessor` reports
  `constraintName.Split('_').Last().ToLowerInvariant()`.
- **`frontend-contract.md`** owns: the mirrored `allow.ts` entries of §2.1, the `error.server.<code>`
  keys for the twelve codes of §3 in all eight locale files, the DTO interfaces mirroring every
  request/response named here, and the `AccountOwned` flag on the upload call for profile images
  (§6.5).
- **`test-plan.md`** should note that the plan's test table places `TenantSwitchTests` and
  `TenantOnboardTests` under `Tests/Features/Identity/Endpoints/Account/`, while the plan's
  workstream 2 - which this contract follows - places both endpoints in
  `Features/Tenancy/Endpoints/Tenants/`. Tests should follow the endpoints, i.e.
  `Tests/Features/Tenancy/Endpoints/Tenants/`.

## 10. Open questions

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

