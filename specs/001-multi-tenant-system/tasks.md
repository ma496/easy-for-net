# Tasks - Multi-tenant system

Source documents: `spec.md` (149 acceptance criteria), `plan.md`, `data-model.md`, `api-contract.md`,
`frontend-contract.md`, `test-plan.md`.

Format is fixed by the `spec-driven` skill: `[P]` marks a task that may run beside its siblings,
`files:` is a hard boundary (the implementing agent edits nothing else), `skill:` names the governing
guide under `.claude/skills/`, `acs:` cites the criteria the task serves, `after:` lists the tasks
that must finish first.

Hotspot files each have a single owning task, and every other task that needs them is sequenced
behind that owner: `src/backend/Source/Data/AppDbContext.cs` (T-019),
`src/backend/Source/Meta.cs` (T-006), `src/backend/Source/Permissions/Allow.cs` (T-023),
`src/backend/Source/ErrorHandling/ErrorCodes.cs` (T-024), `src/backend/Source/Program.cs` (T-031),
`src/frontend/web/allow.ts` (T-080), `auth-urls.ts` (T-081), `nav-items.ts` (T-082),
`searchable-items.ts` (T-083), all eight `public/locales/*.json` (T-084), and each barrel
`index.ts` (T-073, T-075, T-086, T-090, T-093).

## Round 1 - Kernel

Nothing outside this round starts until the named query filters, the tenant accessor and the
save-time attribution rules exist and compile (plan wave 1).

- [ ] **T-001** [P] Add the tenant-scope marker interface - `files:` src/backend/Source/Data/Entities/Base/ITenantScoped.cs - `skill:` backend-entity - `acs:` AC-030, AC-031, AC-033, AC-034, AC-036, AC-037, AC-080, AC-091
- [ ] **T-002** [P] Add the tenant context accessor with explicit tenant, platform and unscoped states - `files:` src/backend/Source/Tenancy/ITenantContext.cs, src/backend/Source/Tenancy/TenantContext.cs - `skill:` backend-feature - `acs:` AC-022, AC-023, AC-035, AC-037, AC-080, AC-131
- [ ] **T-003** [P] Add the bootstrap tenancy constants shared by migration, seeder and tests - `files:` src/backend/Source/Tenancy/TenancyConstants.cs - `skill:` backend-entity - `acs:` AC-082, AC-085
- [ ] **T-004** [P] Add the root-namespace tenancy DTOs the cross-feature contract exchanges - `files:` src/backend/Source/Tenancy/TenancyDtos.cs - `skill:` backend-feature - `acs:` AC-013, AC-025, AC-061, AC-108, AC-139
- [ ] **T-005** [P] Add the tenant scope and attribution exceptions - `files:` src/backend/Source/Exceptions/TenantScopeNotEstablishedException.cs, src/backend/Source/Exceptions/TenantAttributionException.cs - `skill:` backend-entity - `acs:` AC-030, AC-032, AC-035, AC-037, AC-080, AC-131
- [ ] **T-006** Add the tenancy kernel global using - `files:` src/backend/Source/Meta.cs - `skill:` coding-conventions - `acs:` AC-022, AC-030 - `after:` T-002, T-003, T-004
- [ ] **T-007** [P] Add the Tenant entity and the TenantStatus enum - `files:` src/backend/Source/Data/Entities/Tenant.cs - `skill:` backend-entity - `acs:` AC-001, AC-002, AC-005, AC-006, AC-008, AC-009, AC-011, AC-012, AC-065, AC-078, AC-079, AC-100, AC-101, AC-102, AC-144
- [ ] **T-008** [P] Add the TenantMembership entity with its concurrency token - `files:` src/backend/Source/Data/Entities/TenantMembership.cs - `skill:` backend-entity - `acs:` AC-013, AC-014, AC-018, AC-019, AC-020, AC-021, AC-078, AC-079, AC-081, AC-103, AC-106, AC-107 - `after:` T-001
- [ ] **T-009** [P] Configure the Tenants table, its unique identifier index and its lookup indexes - `files:` src/backend/Source/Data/Entities/Configuration/TenantConfiguration.cs - `skill:` backend-entity - `acs:` AC-003, AC-062, AC-064, AC-065, AC-068, AC-102, AC-144, AC-147 - `after:` T-007
- [ ] **T-010** [P] Configure the TenantMemberships table, its filtered unique index and its xmin token - `files:` src/backend/Source/Data/Entities/Configuration/TenantMembershipConfiguration.cs - `skill:` backend-entity - `acs:` AC-015, AC-024, AC-061, AC-062, AC-080, AC-081, AC-106, AC-107, AC-116 - `after:` T-008
- [ ] **T-011** [P] Add the StoredFile entity recording tenant and owner attribution - `files:` src/backend/Source/Features/FileManagement/Core/Entities/StoredFile.cs - `skill:` backend-entity - `acs:` AC-057, AC-058, AC-059, AC-060, AC-097, AC-098, AC-099, AC-128 - `after:` T-001
- [ ] **T-012** [P] Configure the StoredFiles table and its indexes - `files:` src/backend/Source/Features/FileManagement/Core/Entities/Configuration/StoredFileConfiguration.cs - `skill:` backend-entity - `acs:` AC-057, AC-058, AC-059, AC-097 - `after:` T-011
- [ ] **T-013** [P] Give Role a tenant scope and soft-delete semantics - `files:` src/backend/Source/Features/Identity/Core/Entities/Role.cs - `skill:` backend-entity - `acs:` AC-038, AC-039, AC-042, AC-043, AC-110, AC-111, AC-112, AC-113, AC-121, AC-144, AC-147 - `after:` T-001
- [ ] **T-014** [P] Replace the role name index with a per-tenant unique index using NULLS NOT DISTINCT - `files:` src/backend/Source/Features/Identity/Core/Entities/Configuration/RoleConfiguration.cs - `skill:` backend-entity - `acs:` AC-039, AC-068, AC-110, AC-144, AC-147 - `after:` T-013
- [ ] **T-015** [P] Make Notification tenant-scoped with tenant-wide and platform-wide addressing - `files:` src/backend/Source/Features/Notifications/Core/Entities/Notification.cs - `skill:` backend-entity - `acs:` AC-052, AC-053, AC-054, AC-055, AC-056, AC-129, AC-130 - `after:` T-001
- [ ] **T-016** [P] Replace the notification user index with a tenant and user composite index - `files:` src/backend/Source/Features/Notifications/Core/Entities/Configuration/NotificationConfiguration.cs - `skill:` backend-entity - `acs:` AC-052, AC-055, AC-056 - `after:` T-015
- [ ] **T-017** [P] Record the session's tenant on the refresh-token row - `files:` src/backend/Source/Features/Identity/Core/Entities/AuthToken.cs - `skill:` backend-entity - `acs:` AC-108, AC-109, AC-139, AC-140
- [ ] **T-018** [P] Add AcrossAllTenants as the only sanctioned tenant opt-out - `files:` src/backend/Source/Extensions/TenantQueryExtension.cs - `skill:` backend-entity - `acs:` AC-034, AC-036, AC-046, AC-054, AC-095, AC-097, AC-113 - `after:` T-001
- [ ] **T-019** Add the named tenant filter, the tenancy DbSets and the save-time attribution rules - `files:` src/backend/Source/Data/AppDbContext.cs - `skill:` backend-entity - `acs:` AC-030, AC-031, AC-032, AC-033, AC-034, AC-036, AC-037, AC-080, AC-091 - `after:` T-001, T-005, T-006, T-007, T-008, T-009, T-010, T-011, T-012, T-013, T-015, T-018
- [ ] **T-020** [P] Give the design-time factory real no-op user and tenant services - `files:` src/backend/Source/Data/AppDbContextFactory.cs - `skill:` backend-entity - `acs:` AC-085 - `after:` T-002, T-019
- [ ] **T-021** [P] Assert every entity is tenant-scoped or exempt with a written reason - `files:` src/backend/Tests/Architect/TenantScopingTests.cs - `skill:` backend-tests - `acs:` AC-091 - `after:` T-019

## Round 2 - Permission tier, error catalogue, bootstrap and migration

T-026 declares the migrations **directory** rather than two file names: the timestamp prefix
`dotnet ef migrations add AddMultiTenancy --project src/backend/Source/Backend.csproj` mints is not
known until the task runs, so the pair it generates plus `AppDbContextModelSnapshot.cs` are the whole
of that task's boundary, and no other task writes anywhere under `src/backend/Source/Migrations/`.

- [ ] **T-022** [P] Carry an in-memory platform tier on the permission definition model - `files:` src/backend/Source/Permissions/PermissionDefinition.cs, src/backend/Source/Permissions/PermissionDefinitionContext.cs, src/backend/Source/Permissions/PermissionDefinitionService.cs - `skill:` permissions - `acs:` AC-040, AC-041, AC-114, AC-115
- [ ] **T-023** [P] Add the eleven tenancy permission constants - `files:` src/backend/Source/Permissions/Allow.cs - `skill:` permissions - `acs:` AC-044, AC-046, AC-047, AC-071, AC-072, AC-083
- [ ] **T-024** [P] Add the twelve tenancy error codes - `files:` src/backend/Source/ErrorHandling/ErrorCodes.cs - `skill:` api-error-handling - `acs:` AC-067, AC-068
- [ ] **T-025** Reconcile the bootstrap tenant, the platform role, the tenant administrator role and the admin membership - `files:` src/backend/Source/Data/DataSeeder.cs - `skill:` backend-entity - `acs:` AC-042, AC-082, AC-083, AC-084, AC-085, AC-121, AC-145 - `after:` T-019, T-020, T-022, T-023
- [ ] **T-026** Add the AddMultiTenancy migration with the ordered backfills and the NULLS NOT DISTINCT index - `files:` src/backend/Source/Migrations/, src/backend/Source/Migrations/AppDbContextModelSnapshot.cs - `skill:` backend-entity - `acs:` AC-085, AC-097, AC-121, AC-144, AC-145 - `after:` T-014, T-016, T-017, T-025

## Round 3 - Claims, session freshness and the single enforcement point

- [ ] **T-027** Carry the active tenant in the session principal's claims - `files:` src/backend/Source/Features/Identity/Core/ClaimConstants.cs, src/backend/Source/Helper.cs - `skill:` backend-endpoint - `acs:` AC-024, AC-027, AC-108, AC-123 - `after:` T-006
- [ ] **T-028** Recompute membership, roles and permissions per request in the session check - `files:` src/backend/Source/Middleware/SessionValidationMiddleware.cs, src/backend/Source/Features/Identity/Core/SessionValidator.cs - `skill:` backend-endpoint - `acs:` AC-020, AC-108, AC-116, AC-117, AC-127 - `after:` T-018, T-027
- [ ] **T-029** [P] Add the AllowNoTenant marker attribute - `files:` src/backend/Source/Attributes/AllowNoTenantAttribute.cs - `skill:` backend-feature - `acs:` AC-022, AC-051, AC-119, AC-122, AC-136
- [ ] **T-030** Add the global tenant-context pre-processor - `files:` src/backend/Source/Processors/TenantContextProcessor.cs - `skill:` backend-endpoint - `acs:` AC-007, AC-022, AC-023, AC-026, AC-029, AC-037, AC-050, AC-070, AC-099 - `after:` T-002, T-018, T-024, T-029
- [ ] **T-031** Register the tenant context and attach the tenant pre-processor - `files:` src/backend/Source/Program.cs - `skill:` backend-endpoint - `acs:` AC-022, AC-023, AC-037 - `after:` T-002, T-030
- [ ] **T-032** [P] Gate the Hangfire dashboard on the platform permission claim - `files:` src/backend/Source/HangfireAuthorizationFilter.cs - `skill:` permissions - `acs:` AC-041, AC-047 - `after:` T-023

## Round 4 - Services

T-033 owns the whole refresh path: `AuthTokenService.SaveTokenAsync` is the only place an `AuthToken`
row is built, so it widens to take the session's tenant and write T-017's new column, and
`ConsumeRefreshTokenAsync` returns that tenant instead of a bool - otherwise `ExecuteDeleteAsync`
destroys the row before `TokenService.SetRenewalPrivilegesAsync`, which holds only a `UserId`, can
read it. T-034 and T-058 are sequenced behind T-033 so they compile against the new signatures.

- [ ] **T-033** Widen the refresh-token service to carry the tenant, and rebuild the refreshed session from it - `files:` src/backend/Source/Features/Identity/Core/AuthTokenService.cs, src/backend/Source/Features/Identity/Endpoints/Account/TokenService.cs - `skill:` backend-endpoint - `acs:` AC-108, AC-109, AC-124, AC-139 - `after:` T-017, T-027, T-028
- [ ] **T-034** Add the single cross-feature tenant authorization contract and register it - `files:` src/backend/Source/Features/Identity/Core/TenantAuthorizationService.cs, src/backend/Source/Features/Identity/IdentityFeature.cs - `skill:` backend-feature - `acs:` AC-014, AC-016, AC-017, AC-019, AC-038, AC-042, AC-061, AC-072, AC-108, AC-109, AC-139 - `after:` T-004, T-013, T-022, T-033
- [ ] **T-035** Scope user roles, permissions and membership lookups in the user service - `files:` src/backend/Source/Features/Identity/Core/UserService.cs - `skill:` backend-feature - `acs:` AC-027, AC-093, AC-095, AC-096 - `after:` T-013, T-018, T-019
- [ ] **T-036** Make role deletion soft and role lookups tenant-aware in the role service - `files:` src/backend/Source/Features/Identity/Core/RoleService.cs - `skill:` backend-feature - `acs:` AC-110, AC-111, AC-113, AC-144, AC-147 - `after:` T-013, T-014, T-018
- [ ] **T-037** Add the tenant service owning the shared create path - `files:` src/backend/Source/Features/Tenancy/Core/TenantService.cs - `skill:` backend-feature - `acs:` AC-001, AC-002, AC-042, AC-102, AC-134, AC-144 - `after:` T-007, T-009, T-024, T-034
- [ ] **T-038** Add the membership service owning the last-administrator guard - `files:` src/backend/Source/Features/Tenancy/Core/TenantMembershipService.cs - `skill:` backend-feature - `acs:` AC-013, AC-018, AC-019, AC-020, AC-103, AC-106, AC-107 - `after:` T-008, T-010, T-024, T-034
- [ ] **T-039** Add the Tenancy feature module - `files:` src/backend/Source/Features/Tenancy/TenancyFeature.cs - `skill:` backend-feature - `acs:` AC-001, AC-013 - `after:` T-037, T-038
- [ ] **T-040** Declare the tenancy permission hierarchy with its platform tier - `files:` src/backend/Source/Features/Tenancy/Core/TenancyPermissionsProvider.cs - `skill:` permissions - `acs:` AC-040, AC-041, AC-042, AC-044, AC-045, AC-046, AC-047, AC-114, AC-115 - `after:` T-022, T-023
- [ ] **T-041** [P] Add the shared tenant name and identifier validation rules - `files:` src/backend/Source/Features/Tenancy/Core/TenantValidationRules.cs - `skill:` backend-endpoint - `acs:` AC-004, AC-100, AC-101, AC-134
- [ ] **T-042** [P] Add the tenants route group - `files:` src/backend/Source/Features/Tenancy/Endpoints/Tenants/TenantsGroup.cs - `skill:` backend-endpoint - `acs:` AC-071, AC-072
- [ ] **T-043** Add tenant-wide addressing and tenant scoping to the notification service - `files:` src/backend/Source/Features/Notifications/Core/NotificationService.cs - `skill:` notifications - `acs:` AC-052, AC-053, AC-054, AC-129 - `after:` T-015, T-016, T-018, T-019
- [ ] **T-044** Record and resolve file attribution in the file service - `files:` src/backend/Source/Features/FileManagement/Core/FileService.cs - `skill:` file-storage - `acs:` AC-057, AC-058, AC-059, AC-060, AC-097, AC-098, AC-128 - `after:` T-011, T-012, T-019, T-024

## Round 5 - Tenancy endpoints

Each depends on the slice furniture (T-037..T-042), the error codes (T-024) and the AllowNoTenant
marker (T-029); they are file-disjoint from one another.

- [ ] **T-045** [P] Add the tenant list endpoint - `files:` src/backend/Source/Features/Tenancy/Endpoints/Tenants/TenantListEndpoint.cs - `skill:` backend-endpoint - `acs:` AC-046, AC-061, AC-062, AC-063, AC-064, AC-065, AC-066, AC-071 - `after:` T-029, T-037, T-040, T-042
- [ ] **T-046** [P] Add the tenant read endpoint - `files:` src/backend/Source/Features/Tenancy/Endpoints/Tenants/TenantGetEndpoint.cs - `skill:` backend-endpoint - `acs:` AC-010, AC-012, AC-046, AC-066, AC-071, AC-078 - `after:` T-029, T-037, T-040, T-042
- [ ] **T-047** [P] Add the tenant create endpoint - `files:` src/backend/Source/Features/Tenancy/Endpoints/Tenants/TenantCreateEndpoint.cs - `skill:` backend-endpoint - `acs:` AC-001, AC-002, AC-003, AC-004, AC-012, AC-042, AC-071, AC-100, AC-101, AC-102, AC-144 - `after:` T-029, T-037, T-040, T-041, T-042
- [ ] **T-048** [P] Add the tenant update endpoint - `files:` src/backend/Source/Features/Tenancy/Endpoints/Tenants/TenantUpdateEndpoint.cs - `skill:` backend-endpoint - `acs:` AC-003, AC-004, AC-005, AC-010, AC-011, AC-012, AC-071, AC-078, AC-144 - `after:` T-029, T-037, T-040, T-041, T-042
- [ ] **T-049** [P] Add the tenant suspend endpoint - `files:` src/backend/Source/Features/Tenancy/Endpoints/Tenants/TenantSuspendEndpoint.cs - `skill:` backend-endpoint - `acs:` AC-006, AC-007, AC-010, AC-011, AC-020, AC-029, AC-071 - `after:` T-029, T-037, T-040, T-042
- [ ] **T-050** [P] Add the tenant reactivate endpoint - `files:` src/backend/Source/Features/Tenancy/Endpoints/Tenants/TenantReactivateEndpoint.cs - `skill:` backend-endpoint - `acs:` AC-008, AC-010, AC-071 - `after:` T-029, T-037, T-040, T-042
- [ ] **T-051** [P] Add the tenant delete endpoint - `files:` src/backend/Source/Features/Tenancy/Endpoints/Tenants/TenantDeleteEndpoint.cs - `skill:` backend-endpoint - `acs:` AC-009, AC-010, AC-011, AC-029, AC-071, AC-079 - `after:` T-029, T-037, T-040, T-042
- [ ] **T-052** [P] Add the tenant member list endpoint - `files:` src/backend/Source/Features/Tenancy/Endpoints/Tenants/TenantMemberListEndpoint.cs - `skill:` backend-endpoint - `acs:` AC-013, AC-021, AC-046, AC-061, AC-062, AC-063, AC-064, AC-072 - `after:` T-029, T-034, T-038, T-040, T-042
- [ ] **T-053** [P] Add the tenant member add endpoint - `files:` src/backend/Source/Features/Tenancy/Endpoints/Tenants/TenantMemberAddEndpoint.cs - `skill:` backend-endpoint - `acs:` AC-013, AC-014, AC-015, AC-016, AC-042, AC-072, AC-078, AC-106, AC-107 - `after:` T-029, T-034, T-038, T-040, T-042
- [ ] **T-054** [P] Add the tenant member role replacement endpoint - `files:` src/backend/Source/Features/Tenancy/Endpoints/Tenants/TenantMemberUpdateRolesEndpoint.cs - `skill:` backend-endpoint - `acs:` AC-013, AC-017, AC-019, AC-072, AC-081, AC-116 - `after:` T-029, T-034, T-038, T-040, T-042
- [ ] **T-055** [P] Add the tenant member remove endpoint - `files:` src/backend/Source/Features/Tenancy/Endpoints/Tenants/TenantMemberRemoveEndpoint.cs - `skill:` backend-endpoint - `acs:` AC-013, AC-018, AC-019, AC-020, AC-072, AC-079 - `after:` T-029, T-034, T-038, T-040, T-042
- [ ] **T-056** [P] Add the self-service tenant onboarding endpoint - `files:` src/backend/Source/Features/Tenancy/Endpoints/Tenants/TenantOnboardEndpoint.cs - `skill:` backend-endpoint - `acs:` AC-042, AC-120, AC-133, AC-134, AC-135, AC-136, AC-137, AC-139, AC-143 - `after:` T-029, T-034, T-037, T-040, T-041, T-042
- [ ] **T-057** [P] Add the tenant switch endpoint - `files:` src/backend/Source/Features/Tenancy/Endpoints/Tenants/TenantSwitchEndpoint.cs - `skill:` backend-endpoint - `acs:` AC-007, AC-010, AC-025, AC-026, AC-027, AC-029, AC-108, AC-109, AC-127, AC-138, AC-139, AC-140, AC-149 - `after:` T-029, T-034, T-038, T-040, T-042

## Round 6 - Existing endpoints gaining tenant predicates

- [ ] **T-058** [P] Issue the tenant claim on sign-in only for a single active membership - `files:` src/backend/Source/Features/Identity/Endpoints/Account/TokenEndpoint.cs - `skill:` backend-endpoint - `acs:` AC-024, AC-048, AC-049, AC-050, AC-104, AC-123, AC-140, AC-142 - `after:` T-029, T-033, T-035
- [ ] **T-059** [P] Return the caller's tenants, active tenant and platform tier from get-info - `files:` src/backend/Source/Features/Identity/Endpoints/Account/GetInfoEndpoint.cs - `skill:` backend-endpoint - `acs:` AC-024, AC-027, AC-050, AC-073, AC-108, AC-122, AC-123, AC-127, AC-142 - `after:` T-029, T-035
- [ ] **T-060** [P] Stop assigning the retired public role on sign-up - `files:` src/backend/Source/Features/Identity/Endpoints/Account/SignupEndpoint.cs - `skill:` backend-endpoint - `acs:` AC-118, AC-119, AC-120, AC-122 - `after:` T-029
- [ ] **T-061** [P] Exempt the account self-service and anonymous flows from the tenant check - `files:` src/backend/Source/Features/Identity/Endpoints/Account/SignoutEndpoint.cs, src/backend/Source/Features/Identity/Endpoints/Account/ChangePasswordEndpoint.cs, src/backend/Source/Features/Identity/Endpoints/Account/ProfileEndpoint.cs, src/backend/Source/Features/Identity/Endpoints/Account/UpdateProfileEndpoint.cs, src/backend/Source/Features/Identity/Endpoints/Account/ForgetPasswordEndpoint.cs, src/backend/Source/Features/Identity/Endpoints/Account/ResetPasswordEndpoint.cs, src/backend/Source/Features/Identity/Endpoints/Account/VerifyEmailEndpoint.cs, src/backend/Source/Features/Identity/Endpoints/Account/ResendVerifyEmailEndpoint.cs - `skill:` backend-endpoint - `acs:` AC-051, AC-097, AC-119, AC-122, AC-125 - `after:` T-029, T-044
- [ ] **T-062** [P] Restrict the user read, update and delete surfaces to the active tenant - `files:` src/backend/Source/Features/Identity/Endpoints/Users/UserListEndpoint.cs, src/backend/Source/Features/Identity/Endpoints/Users/UserGetEndpoint.cs, src/backend/Source/Features/Identity/Endpoints/Users/UserUpdateEndpoint.cs, src/backend/Source/Features/Identity/Endpoints/Users/UserDeleteEndpoint.cs - `skill:` backend-endpoint - `acs:` AC-093, AC-094, AC-095 - `after:` T-035, T-036
- [ ] **T-063** [P] Create the new account's membership in the active tenant - `files:` src/backend/Source/Features/Identity/Endpoints/Users/UserCreateEndpoint.cs - `skill:` backend-endpoint - `acs:` AC-030, AC-048, AC-096 - `after:` T-008, T-019, T-035, T-036
- [ ] **T-064** [P] Restrict the role read surfaces to the active tenant and honour the platform-only tenant filter - `files:` src/backend/Source/Features/Identity/Endpoints/Roles/RoleListEndpoint.cs, src/backend/Source/Features/Identity/Endpoints/Roles/RoleGetEndpoint.cs - `skill:` backend-endpoint - `acs:` AC-014, AC-017, AC-038, AC-046, AC-110, AC-111, AC-113 - `after:` T-036, T-040
- [ ] **T-065** [P] Attribute, rename, delete and re-permission roles per tenant - `files:` src/backend/Source/Features/Identity/Endpoints/Roles/RoleCreateEndpoint.cs, src/backend/Source/Features/Identity/Endpoints/Roles/RoleUpdateEndpoint.cs, src/backend/Source/Features/Identity/Endpoints/Roles/RoleDeleteEndpoint.cs, src/backend/Source/Features/Identity/Endpoints/Roles/ChangePermissionsEndpoint.cs - `skill:` backend-endpoint - `acs:` AC-039, AC-041, AC-043, AC-111, AC-112, AC-144, AC-147 - `after:` T-022, T-024, T-036
- [ ] **T-066** [P] Filter the permission catalogue by the caller's tier - `files:` src/backend/Source/Features/Identity/Endpoints/Permissions/GetDefinePermissionsEndpoint.cs - `skill:` permissions - `acs:` AC-114, AC-115 - `after:` T-022, T-040
- [ ] **T-067** [P] Scope the notification list and unread count to the active tenant - `files:` src/backend/Source/Features/Notifications/Endpoints/Notifications/NotificationListEndpoint.cs, src/backend/Source/Features/Notifications/Endpoints/Notifications/NotificationGetUnreadCountEndpoint.cs - `skill:` notifications - `acs:` AC-052, AC-053, AC-054, AC-056 - `after:` T-043
- [ ] **T-068** [P] Add hand-written tenant predicates to the mark-all-as-read statements - `files:` src/backend/Source/Features/Notifications/Endpoints/Notifications/NotificationMarkAllAsReadEndpoint.cs - `skill:` notifications - `acs:` AC-033, AC-055, AC-130 - `after:` T-043
- [ ] **T-069** [P] Require authentication and enforce file attribution on the file endpoints - `files:` src/backend/Source/Features/FileManagement/Endpoints/Files/FileUploadEndpoint.cs, src/backend/Source/Features/FileManagement/Endpoints/Files/FileGetEndpoint.cs, src/backend/Source/Features/FileManagement/Endpoints/Files/FileDeleteEndpoint.cs - `skill:` file-storage - `acs:` AC-057, AC-058, AC-059, AC-060, AC-097, AC-098, AC-099 - `after:` T-029, T-044

## Round 7 - Web

OQ-1 is resolved the way `api-contract.md` 6.5 decides it: `FileUploadRequest` carries
`bool AccountOwned` (default `false`) and the wire field is `accountOwned` on the existing
`POST /file-management/upload` route - there is no second route. T-076..T-079 are ordinary tasks, not
conditional ones, and the DTO, the query, the `FileUpload` prop and the profile form all use that one
name.

OQ-2 is resolved as its candidate (a): the existing role list accepts an optional tenant to filter
by, honoured only for a caller holding platform-tier authority and ignored for everyone else, so
there is no new route and no cross-feature contract member. T-064 implements the filter, T-145 adds
the optional field to the web role DTO and query, and the member modals (T-102) use the existing role
list as the role-picker option source.

- [ ] **T-070** [P] Add the tenancy DTO interfaces mirroring the backend contracts - `files:` src/frontend/web/store/api/tenancy/tenants/tenants-dtos.ts - `skill:` rtk-query-api - `acs:` AC-001, AC-002, AC-005, AC-006, AC-008, AC-009, AC-013, AC-014, AC-017, AC-018, AC-025, AC-038, AC-046, AC-061, AC-062, AC-064, AC-065, AC-066, AC-072, AC-078, AC-133, AC-139
- [ ] **T-071** [P] Add the tenant status enum - `files:` src/frontend/web/store/api/tenancy/enums.ts - `skill:` rtk-query-api - `acs:` AC-001, AC-065, AC-071
- [ ] **T-072** Inject the thirteen tenancy endpoints into the shared app api - `files:` src/frontend/web/store/api/tenancy/tenants/tenants-api.ts - `skill:` rtk-query-api - `acs:` AC-002, AC-003, AC-005, AC-006, AC-008, AC-009, AC-014, AC-015, AC-016, AC-017, AC-018, AC-019, AC-025, AC-028, AC-038, AC-046, AC-061, AC-064, AC-066, AC-071, AC-072, AC-081, AC-108, AC-132, AC-133, AC-139 - `after:` T-070, T-071
- [ ] **T-073** Add the tenancy feature barrel - `files:` src/frontend/web/store/api/tenancy/index.ts - `skill:` rtk-query-api - `acs:` AC-071, AC-072 - `after:` T-072
- [ ] **T-074** [P] Extend the get-info response with the caller's tenants - `files:` src/frontend/web/store/api/identity/account/account-dtos.ts - `skill:` rtk-query-api - `acs:` AC-024, AC-029, AC-073, AC-123, AC-127, AC-140, AC-142
- [ ] **T-075** Export the tenant info type from the identity barrel - `files:` src/frontend/web/store/api/identity/index.ts - `skill:` rtk-query-api - `acs:` AC-024 - `after:` T-074
- [ ] **T-076** [P] Add the `accountOwned` flag to the file upload DTOs - `files:` src/frontend/web/store/api/file-management/files/files-dtos.ts - `skill:` rtk-query-api - `acs:` AC-051, AC-097, AC-099 - `after:` T-069
- [ ] **T-077** Pass `accountOwned` through the upload query - `files:` src/frontend/web/store/api/file-management/files/files-api.ts - `skill:` rtk-query-api - `acs:` AC-097, AC-099 - `after:` T-076
- [ ] **T-078** Add the `accountOwned` prop to the file upload field - `files:` src/frontend/web/components/ui/form/file-upload.tsx - `skill:` ui-component - `acs:` AC-097 - `after:` T-077
- [ ] **T-079** Mark the profile avatar upload `accountOwned` - `files:` src/frontend/web/app/[lang]/(auth)/profile/_components/update-profile.tsx - `skill:` frontend-page - `acs:` AC-051, AC-097 - `after:` T-078
- [ ] **T-080** Mirror the eleven permission constants on the web - `files:` src/frontend/web/allow.ts - `skill:` permissions - `acs:` AC-044, AC-074 - `after:` T-023
- [ ] **T-081** Guard the four tenant screens and the two tenant-state screens - `files:` src/frontend/web/auth-urls.ts - `skill:` permissions - `acs:` AC-071, AC-072, AC-074, AC-126, AC-142, AC-143 - `after:` T-080
- [ ] **T-082** Register the tenancy navigation entries - `files:` src/frontend/web/nav-items.ts - `skill:` frontend-page - `acs:` AC-071, AC-072, AC-074, AC-126, AC-142 - `after:` T-080
- [ ] **T-083** Register the tenancy global-search entries - `files:` src/frontend/web/searchable-items.ts - `skill:` frontend-page - `acs:` AC-074, AC-141 - `after:` T-080
- [ ] **T-084** Add every new translation key to all eight locale files, English first - `files:` src/frontend/web/public/locales/en.json, src/frontend/web/public/locales/ar.json, src/frontend/web/public/locales/es.json, src/frontend/web/public/locales/fr.json, src/frontend/web/public/locales/hi.json, src/frontend/web/public/locales/ru.json, src/frontend/web/public/locales/ur.json, src/frontend/web/public/locales/zh.json - `skill:` localization - `acs:` AC-069, AC-075, AC-076 - `after:` T-024
- [ ] **T-085** Carry the active tenant, the tenant list and the tenant error in auth state - `files:` src/frontend/web/store/slices/authSlice.ts - `skill:` redux-state - `acs:` AC-024, AC-029, AC-070, AC-073, AC-108, AC-123, AC-125, AC-127, AC-140 - `after:` T-074, T-088
- [ ] **T-086** Export the tenant error actions from the slice barrel - `files:` src/frontend/web/store/slices/index.ts - `skill:` redux-state - `acs:` AC-070 - `after:` T-085
- [ ] **T-087** Turn tenant refusals into a recorded tenant error instead of a sign-out - `files:` src/frontend/web/store/middlewares/rtk-error-middleware.ts - `skill:` redux-state - `acs:` AC-020, AC-029, AC-070 - `after:` T-086
- [ ] **T-088** [P] Widen the client auth state without changing permission evaluation - `files:` src/frontend/web/lib/utils/authentication-and-authorization.ts - `skill:` coding-conventions - `acs:` AC-027, AC-046 - `after:` T-074
- [ ] **T-089** [P] Add the pure tenant landing and stale-selection helpers - `files:` src/frontend/web/lib/utils/tenant-routing.ts - `skill:` coding-conventions - `acs:` AC-126, AC-127, AC-142, AC-143 - `after:` T-074
- [ ] **T-090** Re-export the tenant routing helpers from the utils barrel - `files:` src/frontend/web/lib/utils/index.ts - `skill:` coding-conventions - `acs:` AC-126 - `after:` T-089
- [ ] **T-091** [P] Add the ordered cache-reset action builders for switch and sign-out - `files:` src/frontend/web/store/tenant-cache.ts - `skill:` coding-conventions - `acs:` AC-028, AC-125, AC-132 - `after:` T-074
- [ ] **T-092** Add the header tenant switcher - `files:` src/frontend/web/components/custom/tenant-switcher.tsx - `skill:` ui-component - `acs:` AC-025, AC-029, AC-073, AC-077 - `after:` T-073, T-084, T-085, T-091
- [ ] **T-093** Export the tenant switcher from the custom barrel - `files:` src/frontend/web/components/custom/index.ts - `skill:` ui-component - `acs:` AC-073 - `after:` T-092
- [ ] **T-094** Render the tenant switcher in the application chrome - `files:` src/frontend/web/components/layouts/header.tsx - `skill:` ui-component - `acs:` AC-073 - `after:` T-093
- [ ] **T-095** Drop the cached tenant data on sign-out - `files:` src/frontend/web/components/custom/nav-user.tsx - `skill:` ui-component - `acs:` AC-125 - `after:` T-091
- [ ] **T-096** Route sign-in to the selection, no-tenant or intended screen - `files:` src/frontend/web/app/[lang]/(auth)/signin/_components/signin-form.tsx - `skill:` frontend-page - `acs:` AC-123, AC-126, AC-140, AC-142 - `after:` T-084, T-089, T-090
- [ ] **T-097** Keep a tenantless caller off tenant-scoped paths and react to tenant failures - `files:` src/frontend/web/App.tsx - `skill:` frontend-page - `acs:` AC-020, AC-029, AC-050, AC-051, AC-070, AC-122, AC-126, AC-127, AC-140 - `after:` T-085, T-087, T-089, T-090
- [ ] **T-098** [P] Add the tenant status filter panel and its toolbar button - `files:` src/frontend/web/app/[lang]/admin/(tenancy)/tenants/list/_components/tenant-filter-panel.tsx, src/frontend/web/app/[lang]/admin/(tenancy)/tenants/list/_components/tenant-filter-button.tsx - `skill:` frontend-crud - `acs:` AC-065, AC-077 - `after:` T-073, T-084
- [ ] **T-099** Add the tenant list screen with its permission-gated row actions - `files:` src/frontend/web/app/[lang]/admin/(tenancy)/tenants/list/page.tsx, src/frontend/web/app/[lang]/admin/(tenancy)/tenants/list/_components/tenant-table.tsx - `skill:` frontend-crud - `acs:` AC-006, AC-008, AC-009, AC-011, AC-046, AC-061, AC-062, AC-063, AC-064, AC-065, AC-066, AC-071, AC-074, AC-077, AC-141 - `after:` T-080, T-084, T-098
- [ ] **T-100** [P] Add the tenant create screen - `files:` src/frontend/web/app/[lang]/admin/(tenancy)/tenants/create/page.tsx, src/frontend/web/app/[lang]/admin/(tenancy)/tenants/create/_components/tenant-create-form.tsx - `skill:` frontend-crud - `acs:` AC-002, AC-003, AC-004, AC-068, AC-069, AC-071, AC-077, AC-100, AC-101 - `after:` T-073, T-084
- [ ] **T-101** [P] Add the tenant update screen - `files:` src/frontend/web/app/[lang]/admin/(tenancy)/tenants/update/[id]/page.tsx, src/frontend/web/app/[lang]/admin/(tenancy)/tenants/update/[id]/_components/tenant-update-form.tsx - `skill:` frontend-crud - `acs:` AC-005, AC-011, AC-071, AC-077, AC-100, AC-101 - `after:` T-073, T-084
- [ ] **T-102** [P] Add the member add and role replacement modals - `files:` src/frontend/web/app/[lang]/admin/(tenancy)/tenants/members/[id]/_components/tenant-member-add-modal.tsx, src/frontend/web/app/[lang]/admin/(tenancy)/tenants/members/[id]/_components/tenant-member-roles-modal.tsx - `skill:` frontend-crud - `acs:` AC-014, AC-015, AC-016, AC-017, AC-019, AC-038, AC-077, AC-081, AC-106 - `after:` T-073, T-084, T-145
- [ ] **T-103** Add the tenant members screen - `files:` src/frontend/web/app/[lang]/admin/(tenancy)/tenants/members/[id]/page.tsx, src/frontend/web/app/[lang]/admin/(tenancy)/tenants/members/[id]/_components/tenant-member-table.tsx - `skill:` frontend-crud - `acs:` AC-013, AC-018, AC-019, AC-061, AC-064, AC-072, AC-077 - `after:` T-080, T-084, T-102
- [ ] **T-104** [P] Add the tenant selection screen with its reason banner - `files:` src/frontend/web/app/[lang]/(auth)/select-tenant/page.tsx, src/frontend/web/app/[lang]/(auth)/select-tenant/_components/select-tenant-view.tsx - `skill:` frontend-page - `acs:` AC-024, AC-025, AC-029, AC-070, AC-077, AC-127, AC-140, AC-142 - `after:` T-073, T-084, T-085, T-091
- [ ] **T-105** [P] Add the self-service tenant onboarding form - `files:` src/frontend/web/app/[lang]/(auth)/no-tenant/_components/tenant-onboard-form.tsx - `skill:` frontend-crud - `acs:` AC-077, AC-133, AC-134, AC-135, AC-143 - `after:` T-073, T-084, T-091
- [ ] **T-106** Add the no-tenant screen - `files:` src/frontend/web/app/[lang]/(auth)/no-tenant/page.tsx, src/frontend/web/app/[lang]/(auth)/no-tenant/_components/no-tenant-view.tsx - `skill:` frontend-page - `acs:` AC-050, AC-077, AC-122, AC-126, AC-143 - `after:` T-084, T-105

## Round 8 - Test fixtures

- [ ] **T-107** Add the seeded tenant ids - `files:` src/backend/Tests/Seeder/TestTenants.cs - `skill:` backend-tests - `acs:` AC-082, AC-090 - `after:` T-025, T-026
- [ ] **T-108** Add the single-permission, second-tenant and platform test roles - `files:` src/backend/Tests/Seeder/TestRoles.cs - `skill:` backend-tests - `acs:` AC-083, AC-088 - `after:` T-107
- [ ] **T-109** Add the limited, membership-less and dual-tenant test accounts - `files:` src/backend/Tests/Seeder/TestUsers.cs - `skill:` backend-tests - `acs:` AC-050, AC-122, AC-123, AC-140 - `after:` T-107
- [ ] **T-110** Seed memberships, a second tenant and stored-file rows for seeded images - `files:` src/backend/Tests/Seeder/TestsDataSeeder.cs - `skill:` backend-tests - `acs:` AC-090, AC-098 - `after:` T-108, T-109
- [ ] **T-111** Add the fixture token helper that also selects a tenant - `files:` src/backend/Tests/TestsHelper.cs - `skill:` backend-tests - `acs:` AC-090 - `after:` T-057, T-110
- [ ] **T-112** Give the test base an optional tenant and a switch helper - `files:` src/backend/Tests/AppTestsBase.cs - `skill:` backend-tests - `acs:` AC-025, AC-123 - `after:` T-111
- [ ] **T-113** Add the tenancy test base with the factories every tenancy test uses - `files:` src/backend/Tests/Features/Tenancy/TenancyTestsBase.cs - `skill:` backend-tests - `acs:` AC-090 - `after:` T-112

## Round 9 - Backend tests

- [ ] **T-114** [P] Test the isolation kernel and non-request tenant scope - `files:` src/backend/Tests/Features/Tenancy/Core/TenantFilterTests.cs, src/backend/Tests/Features/Tenancy/Core/BackgroundTenantScopeTests.cs - `skill:` backend-tests - `acs:` AC-030, AC-031, AC-032, AC-033, AC-034, AC-035, AC-036, AC-037, AC-080, AC-131 - `after:` T-113
- [ ] **T-115** [P] Test cross-tenant indistinguishability, uniqueness over retained rows and concurrent role replacement - `files:` src/backend/Tests/Features/Tenancy/Core/TenantIsolationTests.cs, src/backend/Tests/Features/Tenancy/Core/TenantUniquenessTests.cs, src/backend/Tests/Features/Tenancy/Core/TenantConcurrencyTests.cs - `skill:` backend-tests - `acs:` AC-003, AC-032, AC-039, AC-081, AC-087, AC-102, AC-111, AC-144, AC-147 - `after:` T-113
- [ ] **T-116** [P] Test the tenancy schema and the bootstrap seed - `files:` src/backend/Tests/Features/Tenancy/Core/TenantSchemaTests.cs, src/backend/Tests/Features/Tenancy/Core/TenantSeedingTests.cs - `skill:` backend-tests - `acs:` AC-001, AC-082, AC-083, AC-084, AC-085, AC-121, AC-145 - `after:` T-113
- [ ] **T-117** [P] Test membership activation, tenant context resolution and the platform surface - `files:` src/backend/Tests/Features/Tenancy/Core/MembershipActivationTests.cs, src/backend/Tests/Features/Tenancy/Core/TenantContextResolutionTests.cs, src/backend/Tests/Features/Tenancy/Core/PlatformSurfaceTests.cs - `skill:` backend-tests - `acs:` AC-022, AC-023, AC-046, AC-050, AC-103 - `after:` T-113
- [ ] **T-118** [P] Test the tenant list and read endpoints - `files:` src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantListTests.cs, src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantGetTests.cs - `skill:` backend-tests - `acs:` AC-010, AC-021, AC-046, AC-061, AC-062, AC-063, AC-064, AC-065, AC-066, AC-086 - `after:` T-045, T-046, T-113
- [ ] **T-119** [P] Test tenant creation and renaming - `files:` src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantCreateTests.cs, src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantUpdateTests.cs - `skill:` backend-tests - `acs:` AC-002, AC-003, AC-004, AC-005, AC-010, AC-011, AC-012, AC-042, AC-078, AC-086, AC-100, AC-101, AC-102 - `after:` T-047, T-048, T-113
- [ ] **T-120** [P] Test suspension, reactivation, deletion and behaviour while suspended - `files:` src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantSuspendTests.cs, src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantReactivateTests.cs, src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantDeleteTests.cs, src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantSuspensionTests.cs - `skill:` backend-tests - `acs:` AC-006, AC-007, AC-008, AC-009, AC-011, AC-029, AC-060, AC-070, AC-079, AC-086, AC-089 - `after:` T-049, T-050, T-051, T-113
- [ ] **T-121** [P] Test the member list and member addition - `files:` src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantMemberListTests.cs, src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantMemberAddTests.cs - `skill:` backend-tests - `acs:` AC-013, AC-014, AC-015, AC-016, AC-021, AC-061, AC-062, AC-063, AC-064, AC-078, AC-086, AC-106, AC-107 - `after:` T-052, T-053, T-113
- [ ] **T-122** [P] Test role replacement and member removal - `files:` src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantMemberUpdateRolesTests.cs, src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantMemberRemoveTests.cs - `skill:` backend-tests - `acs:` AC-017, AC-018, AC-019, AC-020, AC-079, AC-086 - `after:` T-054, T-055, T-113
- [ ] **T-123** [P] Test self-service onboarding - `files:` src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantOnboardTests.cs - `skill:` backend-tests - `acs:` AC-086, AC-133, AC-134, AC-135, AC-136, AC-137, AC-146 - `after:` T-056, T-113
- [ ] **T-124** [P] Test tenant selection, switching and session re-establishment - `files:` src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantSwitchTests.cs - `skill:` backend-tests - `acs:` AC-025, AC-026, AC-027, AC-086, AC-108, AC-109, AC-123, AC-138, AC-139, AC-140, AC-148, AC-149 - `after:` T-057, T-113
- [ ] **T-125** [P] Test permission gating on every tenancy endpoint with a single-permission role - `files:` src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantPermissionTests.cs - `skill:` backend-tests - `acs:` AC-044, AC-045, AC-086, AC-088 - `after:` T-045, T-046, T-047, T-048, T-049, T-050, T-051, T-052, T-053, T-054, T-055, T-056, T-057, T-113
- [ ] **T-126** [P] Test authorization freshness on an unchanged token - `files:` src/backend/Tests/Middleware/SessionValidationMiddlewareTenantTests.cs - `skill:` backend-tests - `acs:` AC-020, AC-108, AC-116, AC-117, AC-127 - `after:` T-028, T-113
- [ ] **T-127** [P] Test the error-code catalogue and the dashboard gate - `files:` src/backend/Tests/ErrorHandling/TenantErrorCodesTests.cs, src/backend/Tests/HangfireAuthorizationFilterTests.cs - `skill:` backend-tests - `acs:` AC-047, AC-067 - `after:` T-024, T-032, T-113
- [ ] **T-128** [P] Test sign-in without a tenant and what get-info returns - `files:` src/backend/Tests/Features/Identity/Endpoints/Account/TokenTests.cs, src/backend/Tests/Features/Identity/Endpoints/Account/GetInfoTests.cs - `skill:` backend-tests - `acs:` AC-024, AC-048, AC-049, AC-104, AC-105, AC-124 - `after:` T-058, T-059, T-113
- [ ] **T-129** [P] Test sign-up, account self-service and sign-out with no tenant - `files:` src/backend/Tests/Features/Identity/Endpoints/Account/SignupTests.cs, src/backend/Tests/Features/Identity/Endpoints/Account/AccountSelfServiceTests.cs, src/backend/Tests/Features/Identity/Endpoints/Account/SignoutTests.cs, src/backend/Tests/Features/Identity/Endpoints/Account/ChangePasswordTests.cs - `skill:` backend-tests - `acs:` AC-051, AC-097, AC-118, AC-119, AC-120, AC-122, AC-125 - `after:` T-060, T-061, T-069, T-113
- [ ] **T-130** [P] Test that a refresh preserves the active tenant - `files:` src/backend/Tests/Features/Identity/Core/AuthTokenServiceTests.cs - `skill:` backend-tests - `acs:` AC-109 - `after:` T-033, T-113
- [ ] **T-131** [P] Test tenant-scoped user administration - `files:` src/backend/Tests/Features/Identity/Endpoints/Users/UserListTests.cs, src/backend/Tests/Features/Identity/Endpoints/Users/UserGetTests.cs, src/backend/Tests/Features/Identity/Endpoints/Users/UserCreateTests.cs, src/backend/Tests/Features/Identity/Endpoints/Users/UserUpdateTests.cs, src/backend/Tests/Features/Identity/Endpoints/Users/UserDeleteTests.cs - `skill:` backend-tests - `acs:` AC-048, AC-093, AC-094, AC-095, AC-096, AC-105 - `after:` T-062, T-063, T-113
- [ ] **T-132** [P] Test per-tenant role administration and the platform permission refusal - `files:` src/backend/Tests/Features/Identity/Endpoints/Roles/RoleListTests.cs, src/backend/Tests/Features/Identity/Endpoints/Roles/RoleGetTests.cs, src/backend/Tests/Features/Identity/Endpoints/Roles/RoleCreateTests.cs, src/backend/Tests/Features/Identity/Endpoints/Roles/RoleUpdateTests.cs, src/backend/Tests/Features/Identity/Endpoints/Roles/RoleDeleteTests.cs, src/backend/Tests/Features/Identity/Endpoints/Roles/ChangePermissionsTests.cs - `skill:` backend-tests - `acs:` AC-038, AC-039, AC-041, AC-043, AC-078, AC-110, AC-111, AC-112, AC-113 - `after:` T-064, T-065, T-113
- [ ] **T-133** [P] Test the permission catalogue by tier - `files:` src/backend/Tests/Features/Identity/Endpoints/Permissions/GetDefinePermissionsTests.cs - `skill:` backend-tests - `acs:` AC-040, AC-114, AC-115 - `after:` T-066, T-113
- [ ] **T-134** [P] Test notification addressing and per-tenant visibility - `files:` src/backend/Tests/Features/Notifications/Endpoints/Notifications/NotificationTenancyTests.cs, src/backend/Tests/Features/Notifications/Core/NotificationServiceTests.cs - `skill:` backend-tests - `acs:` AC-052, AC-053, AC-054, AC-129 - `after:` T-043, T-067, T-113
- [ ] **T-135** [P] Test the scoped notification list, count and mark-all-as-read statements, opening a tenant scope in the shared arrangement helpers - `files:` src/backend/Tests/Features/Notifications/Endpoints/Notifications/NotificationsTestsBase.cs, src/backend/Tests/Features/Notifications/Endpoints/Notifications/NotificationListTests.cs, src/backend/Tests/Features/Notifications/Endpoints/Notifications/NotificationGetUnreadCountTests.cs, src/backend/Tests/Features/Notifications/Endpoints/Notifications/NotificationMarkAllAsReadTests.cs - `skill:` backend-tests - `acs:` AC-033, AC-052, AC-055, AC-056, AC-130 - `after:` T-067, T-068, T-113
- [ ] **T-136** [P] Test file upload, download and deletion under tenant attribution - `files:` src/backend/Tests/Features/FileManagement/Endpoints/Files/FileUploadTests.cs, src/backend/Tests/Features/FileManagement/Endpoints/Files/FileGetTests.cs, src/backend/Tests/Features/FileManagement/Endpoints/Files/FileDeleteTests.cs - `skill:` backend-tests - `acs:` AC-057, AC-058, AC-059, AC-060, AC-098, AC-099 - `after:` T-069, T-113
- [ ] **T-137** [P] Test the combined cross-tenant file proof and account-owned images - `files:` src/backend/Tests/Features/FileManagement/Core/FileTenancyTests.cs - `skill:` backend-tests - `acs:` AC-097, AC-128 - `after:` T-069, T-113

## Round 10 - Frontend tests

- [ ] **T-138** [P] Test the tenant landing and stale-selection decisions - `files:` src/frontend/web/lib/utils/tenant-routing.test.ts - `skill:` frontend-tests - `acs:` AC-070, AC-126, AC-127, AC-140, AC-142, AC-143 - `after:` T-089, T-090
- [ ] **T-139** [P] Test that the cache is discarded first on switch and on sign-out - `files:` src/frontend/web/store/tenant-cache.test.ts - `skill:` frontend-tests - `acs:` AC-028, AC-125, AC-132 - `after:` T-091
- [ ] **T-140** [P] Test the tenant fields of auth state - `files:` src/frontend/web/store/slices/authSlice.test.ts - `skill:` frontend-tests - `acs:` AC-024, AC-073, AC-123 - `after:` T-085, T-086
- [ ] **T-141** [P] Test that permissions evaluate per acting tenant - `files:` src/frontend/web/lib/utils/authentication-and-authorization.test.ts - `skill:` frontend-tests - `acs:` AC-027, AC-092 - `after:` T-088
- [ ] **T-142** [P] Test that every tenant error code becomes a translated message - `files:` src/frontend/web/lib/utils/api-error-helpers.test.ts - `skill:` frontend-tests - `acs:` AC-068, AC-069, AC-070 - `after:` T-084, T-147
- [ ] **T-143** [P] Test the route guards and the permission mirror values - `files:` src/frontend/web/auth-urls.test.ts, src/frontend/web/allow.test.ts - `skill:` frontend-tests - `acs:` AC-044, AC-071, AC-072, AC-074, AC-141, AC-143 - `after:` T-080, T-081, T-083
- [ ] **T-144** [P] Test translation-key parity across the eight locale files - `files:` src/frontend/web/i18n/locales.test.ts - `skill:` frontend-tests - `acs:` AC-069, AC-075, AC-076 - `after:` T-084

## Round 11 - OQ-2 resolution

Appended after the fact, so the ids follow the existing ones and `after:` fixes the ordering.

The shape T-064 implements is fixed here, since `api-contract.md` predates the decision:
`RoleListRequest` gains an optional `Guid? TenantId`. A caller holding the platform-tier tenant-view
permission may name any tenant and receives that tenant's roles; for every other caller the field is
ignored and the list stays restricted to the active tenant, so no caller can widen their own view by
setting it. The route, the response DTO and the sortable-field whitelist are unchanged, and the
Identity feature keeps `Role` to itself, so no cross-feature contract member is introduced.

- [ ] **T-145** [P] Add the optional tenant filter to the web role list DTO and query - `files:` src/frontend/web/store/api/identity/roles/roles-dtos.ts, src/frontend/web/store/api/identity/roles/roles-api.ts - `skill:` rtk-query-api - `acs:` AC-014, AC-017, AC-038, AC-046 - `after:` T-064
- [ ] **T-146** [P] Test the role list tenant filter for a platform administrator and its refusal to widen a tenant caller's view - `files:` src/backend/Tests/Features/Identity/Endpoints/Roles/RoleListTenantFilterTests.cs - `skill:` backend-tests - `acs:` AC-038, AC-046, AC-086 - `after:` T-064, T-113

## Round 12 - Coverage repairs

Added after the coverage gate. T-147 owns the web error-message helper, which no earlier task
declared even though the tenant refusals arrive on responses whose branch never reads the error code
today.

- [ ] **T-147** Resolve tenant error codes on the non-validation error branches - `files:` src/frontend/web/lib/utils/api-error-helpers.ts - `skill:` api-error-handling - `acs:` AC-068, AC-069, AC-070 - `after:` T-084

## Coverage

Every acceptance criterion in `spec.md` and the tasks that satisfy it.

| AC | Tasks |
| --- | --- |
| AC-001 | T-007, T-037, T-039, T-047, T-070, T-071, T-116 |
| AC-002 | T-007, T-037, T-047, T-070, T-072, T-100, T-119 |
| AC-003 | T-009, T-047, T-048, T-072, T-100, T-115, T-119 |
| AC-004 | T-041, T-047, T-048, T-100, T-119 |
| AC-005 | T-007, T-048, T-070, T-072, T-101, T-119 |
| AC-006 | T-007, T-049, T-070, T-072, T-099, T-120 |
| AC-007 | T-030, T-049, T-057, T-120 |
| AC-008 | T-007, T-050, T-070, T-072, T-099, T-120 |
| AC-009 | T-007, T-051, T-070, T-072, T-099, T-120 |
| AC-010 | T-046, T-048, T-049, T-050, T-051, T-057, T-118, T-119 |
| AC-011 | T-007, T-048, T-049, T-051, T-099, T-101, T-119, T-120 |
| AC-012 | T-007, T-046, T-047, T-048, T-119 |
| AC-013 | T-004, T-008, T-038, T-039, T-052, T-053, T-054, T-055, T-070, T-103, T-121 |
| AC-014 | T-008, T-034, T-053, T-064, T-070, T-072, T-102, T-121, T-145 |
| AC-015 | T-010, T-053, T-072, T-102, T-121 |
| AC-016 | T-034, T-053, T-072, T-102, T-121 |
| AC-017 | T-034, T-054, T-064, T-070, T-072, T-102, T-122, T-145 |
| AC-018 | T-008, T-038, T-055, T-070, T-072, T-103, T-122 |
| AC-019 | T-008, T-034, T-038, T-054, T-055, T-072, T-102, T-103, T-122 |
| AC-020 | T-008, T-028, T-038, T-049, T-055, T-087, T-097, T-122, T-126 |
| AC-021 | T-008, T-052, T-118, T-121 |
| AC-022 | T-002, T-006, T-029, T-030, T-031, T-117 |
| AC-023 | T-002, T-030, T-031, T-117 |
| AC-024 | T-010, T-027, T-058, T-059, T-074, T-075, T-085, T-104, T-128, T-140 |
| AC-025 | T-004, T-057, T-070, T-072, T-092, T-104, T-112, T-124 |
| AC-026 | T-030, T-057, T-124 |
| AC-027 | T-027, T-035, T-057, T-059, T-088, T-124, T-141 |
| AC-028 | T-072, T-091, T-139 |
| AC-029 | T-030, T-049, T-051, T-057, T-074, T-085, T-087, T-092, T-097, T-104, T-120 |
| AC-030 | T-001, T-005, T-006, T-019, T-063, T-114 |
| AC-031 | T-001, T-019, T-114 |
| AC-032 | T-005, T-019, T-114, T-115 |
| AC-033 | T-001, T-019, T-068, T-114, T-135 |
| AC-034 | T-001, T-018, T-019, T-114 |
| AC-035 | T-002, T-005, T-114 |
| AC-036 | T-001, T-018, T-019, T-114 |
| AC-037 | T-001, T-002, T-005, T-019, T-030, T-031, T-114 |
| AC-038 | T-013, T-034, T-064, T-070, T-072, T-102, T-132, T-145, T-146 |
| AC-039 | T-013, T-014, T-065, T-115, T-132 |
| AC-040 | T-022, T-040, T-133 |
| AC-041 | T-022, T-032, T-040, T-065, T-132 |
| AC-042 | T-013, T-025, T-034, T-037, T-040, T-047, T-053, T-056, T-119 |
| AC-043 | T-013, T-065, T-132 |
| AC-044 | T-023, T-040, T-080, T-125, T-143 |
| AC-045 | T-040, T-125 |
| AC-046 | T-018, T-023, T-040, T-045, T-046, T-052, T-064, T-070, T-072, T-088, T-099, T-117, T-118, T-145, T-146 |
| AC-047 | T-023, T-032, T-040, T-127 |
| AC-048 | T-058, T-063, T-128, T-131 |
| AC-049 | T-058, T-128 |
| AC-050 | T-030, T-058, T-059, T-097, T-106, T-109, T-117 |
| AC-051 | T-029, T-061, T-076, T-079, T-097, T-129 |
| AC-052 | T-015, T-016, T-043, T-067, T-134, T-135 |
| AC-053 | T-015, T-043, T-067, T-134 |
| AC-054 | T-015, T-018, T-043, T-067, T-134 |
| AC-055 | T-015, T-016, T-068, T-135 |
| AC-056 | T-015, T-016, T-067, T-135 |
| AC-057 | T-011, T-012, T-044, T-069, T-136 |
| AC-058 | T-011, T-012, T-044, T-069, T-136 |
| AC-059 | T-011, T-012, T-044, T-069, T-136 |
| AC-060 | T-011, T-044, T-069, T-120, T-136 |
| AC-061 | T-004, T-010, T-034, T-045, T-052, T-070, T-072, T-099, T-103, T-118, T-121 |
| AC-062 | T-009, T-010, T-045, T-052, T-070, T-099, T-118, T-121 |
| AC-063 | T-045, T-052, T-099, T-118, T-121 |
| AC-064 | T-009, T-045, T-052, T-070, T-072, T-099, T-103, T-118, T-121 |
| AC-065 | T-007, T-009, T-045, T-070, T-071, T-098, T-099, T-118 |
| AC-066 | T-045, T-046, T-070, T-072, T-099, T-118 |
| AC-067 | T-024, T-127 |
| AC-068 | T-009, T-014, T-024, T-100, T-142, T-147 |
| AC-069 | T-084, T-100, T-142, T-144, T-147 |
| AC-070 | T-030, T-085, T-086, T-087, T-097, T-104, T-120, T-138, T-142, T-147 |
| AC-071 | T-023, T-042, T-045, T-046, T-047, T-048, T-049, T-050, T-051, T-071, T-072, T-073, T-081, T-082, T-099, T-100, T-101, T-143 |
| AC-072 | T-023, T-034, T-042, T-052, T-053, T-054, T-055, T-070, T-072, T-073, T-081, T-082, T-103, T-143 |
| AC-073 | T-059, T-074, T-085, T-092, T-093, T-094, T-140 |
| AC-074 | T-080, T-081, T-082, T-083, T-099, T-143 |
| AC-075 | T-084, T-144 |
| AC-076 | T-084, T-144 |
| AC-077 | T-092, T-098, T-099, T-100, T-101, T-102, T-103, T-104, T-105, T-106 |
| AC-078 | T-007, T-008, T-046, T-048, T-053, T-070, T-119, T-121, T-132 |
| AC-079 | T-007, T-008, T-051, T-055, T-120, T-122 |
| AC-080 | T-001, T-002, T-005, T-010, T-019, T-114 |
| AC-081 | T-008, T-010, T-054, T-072, T-102, T-115 |
| AC-082 | T-003, T-025, T-107, T-116 |
| AC-083 | T-023, T-025, T-108, T-116 |
| AC-084 | T-025, T-116 |
| AC-085 | T-003, T-020, T-025, T-026, T-116 |
| AC-086 | T-118, T-119, T-120, T-121, T-122, T-123, T-124, T-125, T-146 |
| AC-087 | T-115 |
| AC-088 | T-108, T-125 |
| AC-089 | T-120 |
| AC-090 | T-107, T-110, T-111, T-113 |
| AC-091 | T-001, T-019, T-021 |
| AC-092 | T-141 |
| AC-093 | T-035, T-062, T-131 |
| AC-094 | T-062, T-131 |
| AC-095 | T-018, T-035, T-062, T-131 |
| AC-096 | T-035, T-063, T-131 |
| AC-097 | T-011, T-012, T-018, T-026, T-044, T-061, T-069, T-076, T-077, T-078, T-079, T-129, T-137 |
| AC-098 | T-011, T-044, T-069, T-110, T-136 |
| AC-099 | T-011, T-030, T-069, T-076, T-077, T-136 |
| AC-100 | T-007, T-041, T-047, T-100, T-101, T-119 |
| AC-101 | T-007, T-041, T-047, T-100, T-101, T-119 |
| AC-102 | T-007, T-009, T-037, T-047, T-115, T-119 |
| AC-103 | T-008, T-038, T-117 |
| AC-104 | T-058, T-128 |
| AC-105 | T-128, T-131 |
| AC-106 | T-008, T-010, T-038, T-053, T-102, T-121 |
| AC-107 | T-008, T-010, T-038, T-053, T-121 |
| AC-108 | T-004, T-017, T-027, T-028, T-033, T-034, T-057, T-059, T-072, T-085, T-124, T-126 |
| AC-109 | T-017, T-033, T-034, T-057, T-124, T-130 |
| AC-110 | T-013, T-014, T-036, T-064, T-132 |
| AC-111 | T-013, T-036, T-064, T-065, T-115, T-132 |
| AC-112 | T-013, T-065, T-132 |
| AC-113 | T-013, T-018, T-036, T-064, T-132 |
| AC-114 | T-022, T-040, T-066, T-133 |
| AC-115 | T-022, T-040, T-066, T-133 |
| AC-116 | T-010, T-028, T-054, T-126 |
| AC-117 | T-028, T-126 |
| AC-118 | T-060, T-129 |
| AC-119 | T-029, T-060, T-061, T-129 |
| AC-120 | T-056, T-060, T-129 |
| AC-121 | T-013, T-025, T-026, T-116 |
| AC-122 | T-029, T-059, T-060, T-061, T-097, T-106, T-109, T-129 |
| AC-123 | T-027, T-058, T-059, T-074, T-085, T-096, T-109, T-112, T-124, T-140 |
| AC-124 | T-033, T-128 |
| AC-125 | T-061, T-085, T-091, T-095, T-129, T-139 |
| AC-126 | T-081, T-082, T-089, T-090, T-096, T-097, T-106, T-138 |
| AC-127 | T-028, T-057, T-059, T-074, T-085, T-089, T-097, T-104, T-126, T-138 |
| AC-128 | T-011, T-044, T-137 |
| AC-129 | T-015, T-043, T-134 |
| AC-130 | T-015, T-068, T-135 |
| AC-131 | T-002, T-005, T-114 |
| AC-132 | T-072, T-091, T-139 |
| AC-133 | T-056, T-070, T-072, T-105, T-123 |
| AC-134 | T-037, T-041, T-056, T-105, T-123 |
| AC-135 | T-056, T-105, T-123 |
| AC-136 | T-029, T-056, T-123 |
| AC-137 | T-056, T-123 |
| AC-138 | T-057, T-124 |
| AC-139 | T-004, T-017, T-033, T-034, T-056, T-057, T-070, T-072, T-124 |
| AC-140 | T-017, T-057, T-058, T-074, T-085, T-096, T-097, T-104, T-109, T-124, T-138 |
| AC-141 | T-083, T-099, T-143 |
| AC-142 | T-058, T-059, T-074, T-081, T-082, T-089, T-096, T-104, T-138 |
| AC-143 | T-056, T-081, T-089, T-105, T-106, T-138, T-143 |
| AC-144 | T-007, T-009, T-013, T-014, T-026, T-036, T-037, T-047, T-048, T-065, T-115 |
| AC-145 | T-025, T-026, T-116 |
| AC-146 | T-123 |
| AC-147 | T-009, T-013, T-014, T-036, T-065, T-115 |
| AC-148 | T-124 |
| AC-149 | T-057, T-124 |
