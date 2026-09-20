# Verification — Multi-tenant system

> **Partly superseded.** A later change removed per-request session revalidation and the
> no-active-tenant state it existed for. Roles, permissions and the tenant are now decided once, when
> a token is minted, and trusted until it is replaced; the refresh path is the only place a live
> session is re-examined. Sign-in puts an ordinary account into exactly one tenant or refuses with
> `tenantRequired`, sign-up creates the tenant alongside the account, and self-service onboarding,
> `SessionValidator`, `SessionValidationMiddleware`, `TenantRefusal`, `[AllowNoTenant]` and
> `[AllowPlatformNoTenant]` are gone. The criteria about mid-session revalidation and about an
> account acting in no tenant — AC-020, AC-023, AC-050, AC-070, AC-103, AC-109, AC-118 to AC-122,
> AC-136, AC-140, AC-148 among them — describe behaviour the code no longer has. Read
> `CLAUDE.md` for the current design.

Verified on 2026-09-13 against `spec.md` (149 acceptance criteria), `plan.md` and `tasks.md`
(147 tasks, `T-001`..`T-147`).

## Verdict

| Verdict | Count |
| --- | --- |
| satisfied | 135 |
| partial | 14 |
| missing | 0 |

**The run did not converge.** Fourteen criteria are `partial`, none is `missing`. Round 13 in
`tasks.md` (`T-148`..`T-161`) carries the remaining work — thirteen are missing evidence, one
(`AC-125`) is a real defect.

## How this was produced

Ground truth was taken on the working tree as it stands, not on a claim of it:

- `dotnet build src/backend/Source/Backend.csproj` and `.../Tests/Backend.Tests.csproj` — succeeded,
  0 warnings, 0 errors.
- `dotnet test src/backend/Tests/Backend.Tests.csproj` — 338 passed, 0 failed, against a real
  PostgreSQL.
- `npx tsc --noEmit`, `npx vitest run` (115 tests in 9 files), `npx eslint .` — all clean.
- `dotnet ef migrations list` — 16 migrations, `AddMultiTenancy` pending, so the design-time factory
  builds the model.
- Set operations over the documents: 149 criteria all distinct; 147 task ids all distinct; no
  criterion without a task; no task citing an unknown criterion or an unknown `after:` reference;
  the Coverage table has 149 rows and **0** rows disagree with the tasks' own `acs:` declarations;
  the 8 locale files have exactly 434 keys each with no missing or extra key.

Then **17 read-only tracers**, batched by area of `spec.md` in document order, each returning a
verdict per criterion with `path:line` code evidence and `File::Test` evidence.

Then **5 read-only refuters**, one per band of ~28 criteria, each handed the tracer claims for its
band and told to attack them: to name a concrete input, code path or test the claim survives, and to
say what the claim's evidence would still pass under. The workflow's rule governs — a refuter that
names a concrete residual gap is a finding on its own; a refuter that merely cannot confirm needs
agreement before anything is downgraded. That pass downgraded five criteria (**AC-028, AC-030,
AC-047, AC-066, AC-125**) and is why the count is 14 rather than 9.

Two findings arrived with independent corroboration from `tasks.md` itself. The string
`TenantRefusalResultHandler` — the seam that AC-045 and AC-136 are refused at — appears in **no
task line**. Neither does `change-password` — the sign-out path AC-125 is broken on. Neither was
planned and neither was built by a tracked unit of work.

### Reading the matrix

- Backend code paths are relative to `src/backend/Source/`. A bare `Core/…` or `Endpoints/…` path is
  under `Features/Tenancy/` unless the row names another feature.
- Backend test files are relative to `src/backend/Tests/`. Web paths and test files are relative to
  `src/frontend/web/`.
- `File::Test` names a test method, or a comma-separated list of them.
- A verdict is `satisfied` only when every clause of the criterion has both an implementation and a
  test that would fail without it. Evidence that would pass under a broken implementation is not
  evidence.

## The matrix

| AC | Verdict | Code evidence | Test evidence |
| --- | --- | --- | --- |
| AC-001 | satisfied | `Data/Entities/Tenant.cs:9-27; Configuration/TenantConfiguration.cs:30-32` | `Tests/Features/Tenancy/Core/TenantSchemaTests.cs::Tenant_Entity_Shape` |
| AC-002 | satisfied | `Features/Tenancy/Endpoints/Tenants/TenantCreateEndpoint.cs:37-51; Core/TenantService.cs:184-192` | `TenantCreateTests.cs::Valid_Input` |
| AC-003 | satisfied | `Core/TenantService.cs:154-171 (normalized, both filters named)` | `TenantCreateTests.cs::Duplicate_Identifier_Is_Refused; TenantUniquenessTests.cs::Soft_Deleted_Identifier_Is_Still_Reserved` |
| AC-004 | satisfied | `Core/TenantValidationRules.cs:53-73` | `TenantCreateTests.cs::Invalid_Input, ::Name_Length_Rules, ::Identifier_Format_Rules` |
| AC-005 | satisfied | `TenantUpdateEndpoint.cs:57-67` | `TenantUpdateTests.cs::Valid_Input` |
| AC-006 | satisfied | `TenantSuspendEndpoint.cs:59-60` | `TenantSuspendTests.cs::Suspend_Active_Tenant` |
| AC-007 | satisfied | `Core/SessionValidator.cs:74-82; Processors/TenantContextProcessor.cs:77-91; Tenancy/TenantRefusal.cs:47-49` | `TenantSuspensionTests.cs::Suspended_Tenant_Refuses_Tenant_Scoped_Requests` |
| AC-008 | satisfied | `TenantReactivateEndpoint.cs:48-49` | `TenantSuspensionTests.cs::Reactivation_Restores_Member_Access` |
| AC-009 | satisfied | `TenantDeleteEndpoint.cs:64-65; Data/AppDbContext.cs:312-321` | `TenantDeleteTests.cs::Delete_Tenant` |
| AC-010 | satisfied | `TenantGetEndpoint.cs:28,44; TenantUpdateEndpoint.cs:37; TenantDeleteEndpoint.cs:29,51; TenantSuspendEndpoint.cs:27,49; TenantReactivateEndpoint.cs:27,43` | `TenantGetTests.cs::Deleted_Tenant_Is_Refused_As_If_The_Tenant_Did_Not_Exist; TenantUpdateTests.cs::Deleted_And_Unknown_Tenant_Are_Refused_Alike` |
| AC-011 | satisfied | `TenantUpdateEndpoint.cs:42-45; TenantSuspendEndpoint.cs:52-55; TenantDeleteEndpoint.cs:56-59` | `TenantUpdateTests.cs::Cannot_Update_System_Created_Tenant; TenantSuspendTests.cs::Cannot_Suspend_System_Created_Tenant; TenantDeleteTests.cs::Cannot_Delete_System_Created_Tenant` |
| AC-012 | satisfied | `Data/Entities/Base/AuditableEntity.cs:17-20; Data/AppDbContext.cs:150-170,196-216` | `TenantCreateTests.cs::Records_Audit_Fields` |
| AC-013 | satisfied | `Features/Identity/Core/TenantAuthorizationService.cs:233-269; Features/Tenancy/Core/TenantMembershipService.cs:271-304` | `TenantMemberAddTests.cs::Memberships_Are_Independent; TenantMemberUpdateRolesTests.cs::Only_This_Tenants_Assignments_Are_Replaced` |
| AC-014 | satisfied | `TenantMemberAddEndpoint.cs:124-141; TenantMembershipService.cs:189-231` | `TenantMemberAddTests.cs::Valid_Input, ::First_Member_Is_Also_Granted_Tenant_Administration` |
| AC-015 | satisfied | `TenantMembershipService.cs:178-181,314-317; ErrorCodes.cs:40` | `TenantMemberAddTests.cs::Duplicate_Membership, ::Removed_Membership_Does_Not_Block_Re_Add` |
| AC-016 | satisfied | `TenantMemberAddEndpoint.cs:107-111; TenantAuthorizationService.cs:172-179` | `TenantMemberAddTests.cs::Unknown_User` |
| AC-017 | satisfied | `TenantMembershipService.cs:234-268` | `TenantMemberUpdateRolesTests.cs::Valid_Input, ::Duplicate_Roles_Are_Granted_Once` |
| AC-018 | satisfied | `TenantMembershipService.cs:271-304` | `TenantMemberRemoveTests.cs::Valid_Input, ::Removal_Is_Soft` |
| AC-019 | partial | `TenantMembershipService.cs:285,257; TenantAuthorizationService.cs:273-294 (counts permission holders through UserRoles, not merely members)` | `TenantMemberRemoveTests.cs::Cannot_Remove_Last_Administrator; TenantMemberUpdateRolesTests.cs::Cannot_Strip_Last_Administrator_Role` |
| AC-020 | satisfied | `Middleware/SessionValidationMiddleware.cs:39-50; Core/SessionValidator.cs:50-64,93-103` | `Tests/Middleware/SessionValidationMiddlewareTenantTests.cs::Membership_Revocation_Takes_Effect_On_Next_Request` |
| AC-021 | satisfied | `TenantMemberListEndpoint.cs:49-59; TenantMemberAddEndpoint.cs:81-89; TenantMemberRemoveEndpoint.cs:68-74; TenantMemberUpdateRolesEndpoint.cs:86-90` | `Four ::Non_Member_Is_Refused tests (list, add, remove, update-roles)` |
| AC-022 | satisfied | `Tenancy/TenantContext.cs:25-33; Processors/TenantContextProcessor.cs:57,73` | `TenantContextResolutionTests.cs::Request_Acts_In_Exactly_One_Tenant` |
| AC-023 | satisfied | `Processors/TenantContextProcessor.cs:83-92; Tenancy/TenantRefusal.cs:41-56` | `TenantContextResolutionTests.cs::No_Active_Tenant_Is_Refused` |
| AC-024 | satisfied | `Endpoints/Account/GetInfoEndpoint.cs:86-125` | `GetInfoTests.cs::Returns_The_Callers_Tenants; TenantSuspensionTests.cs::Suspension_Keeps_The_Session_And_Offers_Another_Tenant` |
| AC-025 | satisfied | `TenantSwitchEndpoint.cs:76-127; TenantAuthorizationService.cs:406-444` | `TenantSwitchTests.cs::Switch_Applies_To_Subsequent_Requests, ::Valid_Input` |
| AC-026 | satisfied | `TenantSwitchEndpoint.cs:88-103` | `TenantSwitchTests.cs::Cannot_Switch_To_Non_Member_Tenant, ::Stale_Tenant_Authorization_Is_Refused` |
| AC-027 | satisfied | `Core/SessionValidator.cs:90-98` | `TenantSwitchTests.cs::Permissions_Come_Only_From_The_Active_Tenant, ::Selection_Grants_Exactly_That_Tenants_Data` |
| AC-028 | partial | `store/tenant-cache.ts:18-22; dispatch sites components/custom/tenant-switcher.tsx:84,93, app/[lang]/(auth)/select-tenant/_components/select-tenant-view.tsx:64,72, app/[lang]/(auth)/no-tenant/_components/tenant-onboard-form.tsx:68,76` | `store/tenant-cache.test.ts::tenantChangedActions (asserts only the action list its own builder returns)` |
| AC-029 | satisfied | `Core/SessionValidator.cs:74-82; Tenancy/TenantRefusal.cs:41-56; Processors/TenantContextProcessor.cs:83-92; GetInfoEndpoint.cs:86-125` | `TenantSuspensionTests.cs::Suspension_Keeps_The_Session_And_Offers_Another_Tenant, ::Refusal_Explains_And_Keeps_The_Session; MembershipActivationTests.cs::Active_Membership_Definition(TenantDeleted)` |
| AC-030 | partial | `Data/AppDbContext.cs:274-292 (AttributeAddedEntry), :302-309 (RefuseReattribution) — the guard exists and reads correctly` | `TenantFilterTests.cs::Attribution_Is_Applied_On_Save (exercises only the null-TenantId path)` |
| AC-031 | satisfied | `Data/AppDbContext.cs:113-128; Core/TenantService.cs:120-145` | `TenantFilterTests.cs::Reads_Are_Restricted_To_The_Active_Tenant; TenantIsolationTests.cs::Cross_Tenant_Record_Is_Not_Listed; RoleListTenantFilterTests.cs::Tenant_Caller_Is_Answered_From_The_Tenant_It_Acts_In` |
| AC-032 | satisfied | `Core/RoleService.cs:107-114,158-163; Configuration/TenantConfiguration.cs` | `TenantIsolationTests.cs::Cross_Tenant_Read_Responds_As_Missing, ::Cross_Tenant_Update_Is_Refused, ::Cross_Tenant_Delete_Is_Refused, ::Cross_Tenant_Role_Responds_As_Missing` |
| AC-033 | satisfied | `NotificationMarkAllAsReadEndpoint.cs:56-72; Data/DataSeeder.cs:263` | `TenantFilterTests.cs::Bulk_Statements_Are_Restricted; NotificationMarkAllAsReadTests.cs::Raw_Statement_Respects_The_Tenant, ::Raw_Statement_Leaves_Other_Tenants_Unread` |
| AC-034 | satisfied | `Data/AppDbContext.cs:27,34,80-81,84-98,113-128; Extensions/TenantQueryExtension.cs:22` | `TenantFilterTests.cs::Tenant_And_Soft_Delete_Filters_Coexist` |
| AC-035 | satisfied | `Tenancy/TenantContext.cs:17-19,25-39; Program.cs:54` | `BackgroundTenantScopeTests.cs::Background_Work_Requires_An_Explicit_Tenant, ::Job_Without_A_Tenant_Fails_And_With_One_Processes_Exactly_That_Tenant` |
| AC-036 | satisfied | `Extensions/TenantQueryExtension.cs:21-22; three named IgnoreQueryFilters sites` | `TenantFilterTests.cs::AcrossAllTenants_Is_The_Named_Opt_Out` |
| AC-037 | satisfied | `Data/AppDbContext.cs:120-122; Tenancy/TenantContext.cs:17-19` | `TenantFilterTests.cs::Unresolved_Scope_Fails_Rather_Than_Returning_Everything, ::Unattributed_Write_Is_Rejected` |
| AC-038 | satisfied | `Features/Identity/Core/Entities/Role.cs:11-13; Configuration/RoleConfiguration.cs:30-33` | `RoleCreateTests.cs::Same_Role_Name_In_Two_Tenants, ::Duplicate_Name_In_Same_Tenant_Ignoring_Case` |
| AC-039 | partial | `RoleUpdateEndpoint.cs:58-72 (normalized, names both filters, explicit tenant predicate); RoleCreateEndpoint.cs:43-53` | `RoleCreateTests.cs::Duplicate_Name_In_Same_Tenant_Ignoring_Case; TenantUniquenessTests.cs::Deleted_Role_Name_Is_Still_Reserved` |
| AC-040 | satisfied | `Permissions/PermissionDefinitionService.cs:41-63; PermissionDefinition.cs:15-49` | `GetDefinePermissionsTests.cs::Catalogue_Is_Global_And_Not_Extensible` |
| AC-041 | satisfied | `Endpoints/Roles/ChangePermissionsEndpoint.cs:96-111; PermissionDefinitionService.cs:54-58` | `ChangePermissionsTests.cs::Platform_Permission_Cannot_Be_Granted_To_A_Tenant_Role` |
| AC-042 | satisfied | `TenantAuthorizationService.cs:182-229; Features/Tenancy/Core/TenantService.cs:197-215` | `TenantCreateTests.cs::Provisions_A_System_Created_Administrator_Role; TenantMemberAddTests.cs::First_Member_Is_Also_Granted_Tenant_Administration` |
| AC-043 | satisfied | `RoleDeleteEndpoint.cs:38-39; ChangePermissionsEndpoint.cs:47-48; ErrorCodes.cs:28-29` | `RoleDeleteTests.cs::Cannot_Delete_System_Created_Tenant_Role; ChangePermissionsTests.cs::Cannot_Change_System_Created_Tenant_Role_Permissions` |
| AC-044 | satisfied | `Permissions/Allow.cs:23-35; TenancyPermissionsProvider.cs:21-39; Permissions(Allow.X) on 11 endpoints` | `TenantPermissionTests.cs::Every_Tenant_Endpoint_Declares_Its_Permission (reads the live routing table); src/frontend/web/allow.test.ts` |
| AC-045 | partial | `Middleware/TenantRefusalResultHandler.cs:98 returns null when the tenant is Active, deferring to the framework; that is the only IAuthorizationMiddlewareResultHandler in the backend` | `TenantPermissionTests.cs::Missing_Permission_Is_Forbidden (status code only; the test file itself records that the code half is unasserted)` |
| AC-046 | satisfied | `Features/Tenancy/Core/TenantService.cs:124-127; Core/RoleService.cs:130-133` | `PlatformSurfaceTests.cs::Platform_Administrator_Administers_A_Tenant_It_Is_Not_A_Member_Of; TenantListTests.cs::Platform_Administrator_Sees_Tenants_It_Is_Not_A_Member_Of` |
| AC-047 | partial | `HangfireAuthorizationFilter.cs:33-45; Program.cs:241-243 (the only wiring)` | `HangfireAuthorizationFilterTests.cs::Dashboard_Requires_Platform_Administration (five direct IsAuthorized calls over fabricated principals)` |
| AC-048 | satisfied | `Entities/User.cs:8 (not ITenantScoped); Configuration/UserConfiguration.cs:21-26; Migrations/AppDbContextModelSnapshot.cs:443-453` | `UserCreateTests.cs::Username_And_Email_Stay_Globally_Unique; SignupTests.cs::Creates_A_Global_Account_With_No_Membership` |
| AC-049 | satisfied | `Endpoints/Account/TokenEndpoint.cs:37-62,115-128,150-156` | `TokenTests.cs::Sign_In_Does_Not_Name_A_Tenant` |
| AC-050 | satisfied | `Processors/TenantContextProcessor.cs:61-91; Tenancy/TenantRefusal.cs:53-55` | `TenantContextResolutionTests.cs::Account_With_No_Membership_Is_Refused_With_An_Explanation` |
| AC-051 | satisfied | `[AllowNoTenant] on all ten Account endpoints; FileUploadEndpoint.cs:22,43-49` | `AccountSelfServiceTests.cs::Self_Service_Flows_Work_Without_A_Tenant (seven-flow theory); ChangePasswordTests.cs::Works_Without_An_Active_Tenant` |
| AC-052 | satisfied | `NotificationService.cs:114-149,191-194; NotificationQueries.cs:29-32` | `NotificationTenancyTests.cs::Tenant_Notification_Is_Visible_Only_In_Its_Tenant; NotificationListTests.cs::Notification_Of_Another_Tenant_Is_Not_Listed` |
| AC-053 | satisfied | `NotificationService.cs:114-129,134-149; Entities/Notification.cs:20-27` | `NotificationServiceTests.cs::Addresses_A_Single_Member, ::Addresses_Every_Member_Of_The_Tenant` |
| AC-054 | satisfied | `NotificationService.cs:162-179; Entities/Notification.cs:32; NotificationQueries.cs:29-32` | `NotificationTenancyTests.cs::Platform_Wide_Notification_Is_Visible_In_Every_Tenant; NotificationServiceTests.cs::Keeps_A_Platform_Wide_Notification_Distinguishable_From_A_Tenant_Wide_One` |
| AC-055 | satisfied | `NotificationMarkAllAsReadEndpoint.cs:54-72` | `NotificationMarkAllAsReadTests.cs::Marks_Only_The_Active_Tenants_Notifications, ::Raw_Statement_Respects_The_Tenant, ::Raw_Statement_Leaves_Other_Tenants_Unread` |
| AC-056 | satisfied | `NotificationService.cs:89-108; NotificationGetUnreadCountEndpoint.cs:34` | `NotificationGetUnreadCountTests.cs::Counts_Only_The_Active_Tenant; NotificationTenancyTests.cs::Notification_In_One_Tenant_Is_Neither_Listed_Nor_Counted_In_Another` |
| AC-057 | satisfied | `FileService.cs:141-148,327-330; Data/AppDbContext.cs:274-293` | `FileUploadTests.cs::Upload_Is_Attributed_To_The_Active_Tenant` |
| AC-058 | satisfied | `FileService.cs:252-294; FileGetEndpoint.cs:63-88; FileDeleteEndpoint.cs:63-88` | `FileGetTests.cs::Cross_Tenant_File_Is_Refused; FileDeleteTests.cs::Cross_Tenant_Delete_Is_Refused` |
| AC-059 | satisfied | `FileService.cs:190-193,221-225,254-256; Entities/StoredFile.cs:22-24; FileGetEndpoint.cs:108-111` | `FileGetTests.cs::Guessed_Stored_Name_Does_Not_Yield_Another_Tenants_Content` |
| AC-060 | partial | `FileService.cs:276-289 (TenantNotFound for deleted, TenantSuspended for suspended, no content on either); FileGetEndpoint.cs:81-82; TenantDeleteEndpoint.cs:61-65` | `TenantSuspensionTests.cs::Suspended_Tenant_Files_Are_Not_Served_But_Are_Retained (suspension only)` |
| AC-061 | satisfied | `Base/Dto/ListRequestDtoValidator.cs:18-19; Extensions/IQueryableExtension.cs:53-54; TenantListEndpoint.cs:84; TenantMemberListEndpoint.cs:97` | `TenantListTests.cs::List_Tenants_Pagination, ::Page_Size_Beyond_The_Maximum_Is_Refused; TenantMemberListTests.cs::List_Members_Pagination` |
| AC-062 | satisfied | `TenantListEndpoint.cs:91-95; IQueryableExtension.cs:28-38; TenantMemberListEndpoint.cs:103-107` | `TenantListTests.cs::Sorting; TenantMemberListTests.cs::Sorting` |
| AC-063 | satisfied | `TenantListEndpoint.cs:91-95; TenantMemberListEndpoint.cs:103-107` | `TenantListTests.cs::Invalid_Sort_Field; TenantMemberListTests.cs::Invalid_Sort_Field` |
| AC-064 | satisfied | `TenantListEndpoint.cs:40-42; TenantAuthorizationService.cs:315-320` | `TenantListTests.cs::Search_By_Name_And_Identifier; TenantMemberListTests.cs::Search_By_Username_And_Email` |
| AC-065 | satisfied | `TenantListEndpoint.cs:45-48,73,85-87` | `TenantListTests.cs::Filter_By_Status` |
| AC-066 | partial | `Features/Tenancy/Core/TenantService.cs:139-144 (AcrossAllTenants on memberships, soft-delete filter in force)` | `TenantListTests.cs::Non_Platform_Caller_Sees_Only_Own_Tenants (one joined tenant, one never joined)` |
| AC-067 | satisfied | `ErrorHandling/ErrorCodes.cs:35-43; ThrowError sites across Features/Tenancy/Endpoints/Tenants/*` | `Tests/ErrorHandling/TenantErrorCodesTests.cs::All_Tenant_Error_Codes_Are_Declared` |
| AC-068 | satisfied | `ThrowError(x => x.Field, ...) at TenantCreateEndpoint.cs:40, TenantOnboardEndpoint.cs:64, TenantMemberAddEndpoint.cs:110,119,127, TenantMemberUpdateRolesEndpoint.cs:123, TenantUpdateEndpoint.cs:54; TenantRefusalResultHandler.cs:53-56` | `TenantCreateTests.cs::Duplicate_Identifier_Is_Refused; TenantMemberAddTests.cs::Duplicate_Membership` |
| AC-069 | satisfied | `All 38 ErrorCodes constants vs error.server.<code> in the 8 locale files; api-error-helpers.ts:108-120,149-158` | `src/frontend/web/i18n/locales.test.ts::explains every tenant error code in every locale; api-error-helpers.test.ts` |
| AC-070 | partial | `store/middlewares/rtk-error-middleware.ts:11-27,35-50,71-74; App.tsx:62-71; select-tenant-view.tsx:21-27,89-93,103-128` | `authSlice.test.ts::leaves the caller authenticated in the tenant they were acting in; api-error-helpers.test.ts; TenantSuspensionTests.cs:142-143` |
| AC-071 | satisfied | `tenant-table.tsx:82-87,231-273,310-315; auth-urls.ts:49-63; App.tsx:84-86` | `src/frontend/web/auth-urls.test.ts::guards %s with the permission the screen requires; TenantPermissionTests.cs::Missing_Permission_Is_Forbidden` |
| AC-072 | satisfied | `tenant-member-table.tsx:58-60,127-186; tenant-member-add-modal.tsx:111-117; tenant-member-roles-modal.tsx:100-106; auth-urls.ts:61-63` | `auth-urls.test.ts::guards /admin/tenants/members/{id}; TenantMemberListTests.cs::Rows_Report_The_Member_And_The_Tenant_Roles_It_Holds` |
| AC-073 | satisfied | `components/custom/tenant-switcher.tsx:53,57,63-98,100-144; components/layouts/header.tsx:78` | `store/slices/authSlice.test.ts::takes the active tenant from the account info it was just handed` |
| AC-074 | satisfied | `sidebar/index.tsx:47-70; search-component.tsx:29-35; App.tsx:84-87; nav-items.ts:91-115; searchable-items.ts:33-40` | `auth-urls.test.ts; authentication-and-authorization.test.ts::requires every requested permission` |
| AC-075 | partial | `Exported rows and sheet name are literals: tenant-table.tsx:129-134 (Name, Identifier, 'Tenants'); unreachable English fallbacks: tenant-filter-panel.tsx:33,34,35,50,58,73,82 and tenant-filter-button.tsx:32` | `i18n/locales.test.ts::renders every string of the tenant screens from a key that every locale defines (covers t() calls, not the literals)` |
| AC-076 | satisfied | `public/locales/{ar,en,es,fr,hi,ru,ur,zh}.json — 434 keys each, exact set equality with en.json` | `i18n/locales.test.ts::defines every key in every locale, ::ships a file for every configured locale` |
| AC-077 | partial | `Logical utilities used correctly at tenant-switcher.tsx:133, select-tenant-view.tsx:110, tenant-table.tsx:318, tenant-switcher.tsx:115; one physical utility remains: tenant-filter-button.tsx:34 (ml-1)` | `none — no RTL test exists anywhere in the repo, and tasks.md schedules none` |
| AC-078 | satisfied | `Data/AppDbContext.cs:141,187,243-260` | `TenantCreateTests.cs::Records_Audit_Fields; TenantMemberAddTests.cs::Records_Audit_Fields; TenantMemberUpdateRolesTests.cs:54-69; RoleCreateTests.cs::Records_Audit_Fields` |
| AC-079 | satisfied | `AppDbContext.cs ApplySoftDeleteRules; TenantDeleteEndpoint.cs:64-65; TenantMembershipService.cs:293` | `TenantDeleteTests.cs::Delete_Tenant; TenantMemberRemoveTests.cs::Removal_Is_Soft` |
| AC-080 | satisfied | `AppDbContext.cs:243-280 (throws TenantScopeNotEstablishedException when the scope is unresolved)` | `TenantFilterTests.cs::Unattributed_Write_Is_Rejected` |
| AC-081 | satisfied | `Configuration/TenantMembershipConfiguration.cs (xmin row version); assignment replacement in one transaction` | `TenantConcurrencyTests.cs::Concurrent_Assignment_Updates_Keep_One_Complete_Set, ::Concurrent_Replacement_Is_Detected_By_The_Membership_Token` |
| AC-082 | satisfied | `Data/DataSeeder.cs:55,71; TenancyConstants.cs` | `TenantSeedingTests.cs::Bootstrap_Tenant_Exists_With_The_Seeded_Administrator` |
| AC-083 | satisfied | `DataSeeder.cs:60,146,178` | `TenantSeedingTests.cs::Seeded_Administrator_Is_A_Platform_Administrator` |
| AC-084 | satisfied | `DataSeeder.cs:57,62,102,245` | `TenantSeedingTests.cs::Reconciliation_Preserves_Tenants_Memberships_And_Tenant_Roles, ::No_Public_Role_Is_Seeded` |
| AC-085 | satisfied | `Indexes declared in the model (RoleConfiguration.cs, TenantConfiguration.cs, TenantMembershipConfiguration.cs); tool/EasyForNetTool/Generator/CreateProjectGenerator.cs:86 excludes Migrations` | `TenantSchemaTests.cs::Schema_Is_Complete_From_A_First_Time_Creation (empty GetPendingMigrationsAsync, both indexes in pg_indexes)` |
| AC-086 | satisfied | `13 endpoints under Features/Tenancy/Endpoints/Tenants/` | `One matching test file per endpoint plus TenantSuspensionTests.cs and TenantPermissionTests.cs; the test-plan.md:220-278 branch list resolves against the code` |
| AC-087 | satisfied | `TenantIsolationTests.cs subjects` | `TenantIsolationTests.cs::Cross_Tenant_Read_Responds_As_Missing, ::Cross_Tenant_Update_Is_Refused, ::Cross_Tenant_Delete_Is_Refused, ::Cross_Tenant_Record_Is_Not_Listed, ::Cross_Tenant_Role_Responds_As_Missing (each compares against the answer for a random Guid and re-reads the row)` |
| AC-088 | satisfied | `Permissions(Allow.X) declared on every gated endpoint` | `TenantPermissionTests.cs::Every_Tenant_Endpoint_Declares_Its_Permission, ::Missing_Permission_Is_Forbidden, ::Purpose_Built_Role_Proves_The_Gate (uses TestRoles.LimitedTenantRoleId, which holds exactly Allow.Tenant_View)` |
| AC-089 | satisfied | `SessionValidator.cs:74-82; TenantContextProcessor.cs:77-91` | `TenantSuspensionTests.cs::Suspended_Tenant_Refuses_Tenant_Scoped_Requests, ::Reactivation_Restores_Member_Access, ::Refusal_Explains_And_Keeps_The_Session` |
| AC-090 | satisfied | `Tests/Features/Tenancy/TenancyTestsBase.cs factories derive names from Guid.NewGuid()` | `Every count in the tenancy suite is scoped to rows the test created (TenantListTests.cs:39,92-95; TenantMemberListTests.cs:48-51; TenantIsolationTests.cs:108-111)` |
| AC-091 | satisfied | `Data/AppDbContext.cs TenantFilter registration; ITenantScoped implementers` | `Tests/Architect/TenantScopingTests.cs::Every_Entity_Is_Scoped_Or_Exempt_With_A_Reason, ::Every_Tenant_Scoped_Entity_Carries_The_Tenant_Filter` |
| AC-092 | satisfied | `src/frontend/web/lib/utils/authentication-and-authorization.ts (isAllowed over the roles reported for the tenant acted in)` | `lib/utils/authentication-and-authorization.test.ts::refuses a permission only the other tenant grants` |
| AC-093 | satisfied | `Features/Identity/Core/UserService.cs:141-164; Endpoints/Users/UserListEndpoint.cs:24-27,50-53` | `UserListTests.cs::Users_Are_Restricted_To_The_Active_Tenant (foreign search returns Total == 0)` |
| AC-094 | satisfied | `UserGetEndpoint.cs:23-32; UserUpdateEndpoint.cs:24-31; UserDeleteEndpoint.cs:22-28` | `UserGetTests.cs::Cross_Tenant_User_Responds_As_Missing; UserUpdateTests.cs; UserDeleteTests.cs (each re-reads the other tenant's account)` |
| AC-095 | satisfied | `UserService.cs:146-149; SessionValidator.cs:93-97` | `UserListTests.cs::Platform_Administrator_Sees_Every_Account` |
| AC-096 | satisfied | `UserService.cs:167-186; UserCreateEndpoint.cs:65` | `UserCreateTests.cs::Created_User_Gains_A_Membership_In_The_Active_Tenant` |
| AC-097 | satisfied | `FileService.cs:143-144,266-271,311-317,338-339; FileUploadEndpoint.cs:43-46` | `FileTenancyTests.cs::Account_Owned_File_Is_Readable_In_Any_Tenant_And_In_None; AccountSelfServiceTests.cs::Self_Service_Flows_Work_Without_A_Tenant (profile-image flow)` |
| AC-098 | satisfied | `No [AllowAnonymous] on FileUploadEndpoint.cs:32-37, FileGetEndpoint.cs:47-51, FileDeleteEndpoint.cs:46-51` | `FileUploadTests.cs::Unauthenticated; FileGetTests.cs::Unauthenticated; FileDeleteTests.cs::Unauthenticated` |
| AC-099 | satisfied | `FileUploadEndpoint.cs:43-46; ErrorCodes.cs:38; AppDbContext.cs:274-293` | `FileUploadTests.cs::Upload_Without_An_Active_Tenant_Is_Refused (asserts the storage inventory is byte-identical before and after)` |
| AC-100 | satisfied | `TenantValidationRules.cs:25-28,53-56; mirrors at tenant-create-form.tsx:19-22, tenant-update-form.tsx:18-21, tenant-onboard-form.tsx:24-26` | `TenantCreateTests.cs::Name_Length_Rules` |
| AC-101 | satisfied | `TenantValidationRules.cs:31-34,42,44,68-73; mirrors in the three web forms` | `TenantCreateTests.cs::Identifier_Format_Rules` |
| AC-102 | satisfied | `Data/Entities/Tenant.cs:14,23-26; TenantConfiguration.cs:30-33; TenantService.cs:158-163; TenantListEndpoint.cs:40-42` | `TenantUniquenessTests.cs::Identifier_Is_Stored_Trimmed_With_A_Normalized_Copy; TenantListTests.cs::Search_By_Name_And_Identifier` |
| AC-103 | satisfied | `Configuration/TenantMembershipConfiguration.cs (no status column; partial unique index filtered IsDeleted = false)` | `MembershipActivationTests.cs::Active_Membership_Definition (five-case theory), ::Membership_Carries_No_Further_State (asserts the exact property set)` |
| AC-104 | satisfied | `Core/SessionValidator.cs:50-103` | `TokenTests.cs::Deactivated_Account_Cannot_Authenticate` |
| AC-105 | satisfied | `Core/SessionValidator.cs` | `TokenTests.cs::Reactivated_Account_Keeps_Exactly_Its_Memberships` |
| AC-106 | satisfied | `TenantMembershipService.cs (no state to reactivate)` | `TenantMemberAddTests.cs::Re_Add_After_Removal (re-adds with a different role and asserts the grant is exactly the new role)` |
| AC-107 | satisfied | `Configuration/TenantMembershipConfiguration.cs (partial unique index)` | `TenantMemberAddTests.cs::Removed_Membership_Does_Not_Block_Re_Add` |
| AC-108 | satisfied | `Middleware/SessionValidationMiddleware.cs:39-50,64-87; SessionValidator.cs:32,93-103; TenantAuthorizationService.cs:406` | `SessionValidationMiddlewareTenantTests.cs::Switch_Recomputes_Permissions_At_Request_Time; TenantSwitchTests.cs::Permissions_Come_Only_From_The_Active_Tenant` |
| AC-109 | satisfied | `SessionValidationMiddleware.cs:69-75; SessionValidator.cs:74-87; TenantService.cs:120-146` | `TenantSwitchTests.cs::Stale_Tenant_Authorization_Is_Refused, ::Request_Supplied_Tenant_Is_Ignored` |
| AC-110 | satisfied | `Endpoints/Roles/RoleListEndpoint.cs:31,54; Core/RoleService.cs:123-139` | `RoleListTests.cs::Roles_Are_Restricted_To_The_Active_Tenant (asserts page.Total)` |
| AC-111 | satisfied | `RoleGetEndpoint.cs:30-38; RoleUpdateEndpoint.cs:44-50; RoleDeleteEndpoint.cs:32-37; ChangePermissionsEndpoint.cs:39-46; RoleService.cs:107-114` | `Four Cross_Tenant_Role_Responds_As_Missing tests (status and raw body compared against a random Guid)` |
| AC-112 | satisfied | `RoleCreateEndpoint.cs:43-59,69-73; AppDbContext.cs:274-285` | `RoleCreateTests.cs::Role_Is_Attributed_To_The_Active_Tenant (reflects over the request to prove it carries no tenant)` |
| AC-113 | partial | `Core/RoleService.cs:130-133 (AcrossAllTenants under platform administration); RoleListEndpoint.cs:41-44; get/update/delete/change-permissions inherit it by reading Roles()` | `RoleListTests.cs::Platform_Administrator_Sees_Roles_Across_Tenants; RoleListTenantFilterTests.cs::Platform_Administrator_Narrows_The_List_To_A_Named_Tenant (the list verb only)` |
| AC-114 | satisfied | `GetDefinePermissionsEndpoint.cs:29-30; PermissionDefinitionService.cs:52-54,112-137` | `GetDefinePermissionsTests.cs::Tenant_Caller_Receives_Only_Tenant_Permissions` |
| AC-115 | satisfied | `GetDefinePermissionsEndpoint.cs:29-30; PermissionDefinition.cs:25` | `GetDefinePermissionsTests.cs::Platform_Caller_Receives_The_Whole_Catalogue` |
| AC-116 | satisfied | `SessionValidationMiddleware.cs:39-50,64-87; SessionValidator.cs:93-103; Program.cs:192-195` | `SessionValidationMiddlewareTenantTests.cs::Role_Assignment_Change_Applies_On_Next_Request (same client and token, password hash asserted unchanged)` |
| AC-117 | satisfied | `SessionValidator.cs:93-103; RoleService.cs:170-178 (soft delete)` | `SessionValidationMiddlewareTenantTests.cs::Role_Permission_Change_Applies_On_Next_Request (covers both clauses)` |
| AC-118 | satisfied | `Endpoints/Account/SignupEndpoint.cs (creates inside BeginPlatformScope, adds no membership)` | `SignupTests.cs::Creates_A_Global_Account_With_No_Membership` |
| AC-119 | satisfied | `SignupEndpoint.cs ([AllowAnonymous], [AllowNoTenant])` | `SignupTests.cs::Works_With_No_Tenant_Context` |
| AC-120 | satisfied | `SignupEndpoint.cs; TenantOnboardEndpoint.cs; DataSeeder.cs:178-197` | `SignupTests.cs::Grants_No_Role_And_No_Platform_Permission; TenantOnboardTests.cs::Grants_No_Platform_Permission` |
| AC-121 | satisfied | `DataSeeder.cs:146,178,245` | `TenantSeedingTests.cs::Every_Role_Has_A_Declared_Scope, ::No_Public_Role_Is_Seeded` |
| AC-122 | satisfied | `Processors/TenantContextProcessor.cs:87; [AllowNoTenant] on the self-service and onboard endpoints` | `AccountSelfServiceTests.cs::Self_Service_Flows_Work_Without_A_Tenant (proves the standing by taking the 403 first)` |
| AC-123 | satisfied | `Endpoints/Account/TokenEndpoint.cs:57,62,115-128; Helper.cs; signin-form.tsx:94-98; tenant-routing.ts:75-80` | `TokenTests.cs::Sign_In_Does_Not_Name_A_Tenant; TenantSwitchTests.cs::Single_Membership_Is_Auto_Selected; authSlice.test.ts` |
| AC-124 | satisfied | `App.tsx:30-38,91-117; store/slices/authSlice.ts:31-32; GetInfoEndpoint.cs:57-67; TokenService.cs:104-108,159-160; no tenant in localStorage/sessionStorage` | `GetInfoTests.cs::Active_Tenant_Survives_A_Fresh_Client; AuthTokenServiceTests.cs::Refresh_Preserves_The_Active_Tenant` |
| AC-125 | partial | `store/tenant-cache.ts:31-35 (signedOutActions exists and is correct); dispatched at nav-user.tsx:47 and no-tenant-view.tsx:37 — but NOT at change-password-form.tsx:57, which dispatches bare signout() then router.push('/signin')` | `store/tenant-cache.test.ts::discards the cache, clears the selection and the badge when the user signs out (asserts the builder only); authSlice.test.ts::signout; SignoutTests.cs::Signout_Clears_The_Session_Tenant` |
| AC-126 | satisfied | `app/[lang]/(auth)/no-tenant/page.tsx; no-tenant-view.tsx:48-73; tenant-routing.ts:77; App.tsx:76-82; auth-urls.ts:67-69` | `tenant-routing.test.ts::sends a caller who belongs to no tenant to the screen that explains it` |
| AC-127 | satisfied | `GetInfoEndpoint.cs:57-67; tenant-routing.ts:45-53,78; App.tsx:62-82` | `SessionValidationMiddlewareTenantTests.cs::Stale_Selection_Is_Not_Honoured; TenantSuspensionTests.cs::Suspension_Keeps_The_Session_And_Offers_Another_Tenant; tenant-routing.test.ts` |
| AC-128 | satisfied | `FileService.cs; StoredFile.cs` | `FileTenancyTests.cs::Member_Of_One_Tenant_Cannot_Read_Replace_Or_Delete_Another_Tenants_File, ::Account_Owned_File_Is_Readable_In_Any_Tenant_And_In_None` |
| AC-129 | satisfied | `NotificationQueries.cs:29-32` | `NotificationTenancyTests.cs::Notification_In_One_Tenant_Is_Neither_Listed_Nor_Counted_In_Another, ::Platform_Wide_Notification_Is_Visible_In_Every_Tenant, ::Tenant_Notification_Is_Visible_Only_In_Its_Tenant` |
| AC-130 | satisfied | `NotificationMarkAllAsReadEndpoint.cs:54-72` | `NotificationMarkAllAsReadTests.cs::Raw_Statement_Leaves_Other_Tenants_Unread, ::Marks_Only_The_Active_Tenants_Notifications, ::Raw_Statement_Respects_The_Tenant` |
| AC-131 | satisfied | `Tenancy/TenantContext.cs:17-19,39; AppDbContext.cs:280` | `BackgroundTenantScopeTests.cs::Job_Without_A_Tenant_Fails_And_With_One_Processes_Exactly_That_Tenant, ::Background_Work_Requires_An_Explicit_Tenant` |
| AC-132 | satisfied | `store/tenant-cache.ts:18-22; dispatch sites tenant-switcher.tsx:84,93 and select-tenant-view.tsx:64,72` | `store/tenant-cache.test.ts:25-37 (asserts the ordered triple with resetApiState first), designated as this criterion's evidence at test-plan.md:455` |
| AC-133 | satisfied | `TenantOnboardEndpoint.cs:74-81; Features/Tenancy/Core/TenantService.cs:184-217` | `TenantOnboardTests.cs::Valid_Input` |
| AC-134 | satisfied | `TenantOnboardEndpoint.cs:61-64,109-116; TenantService.cs:154-163; tenant-onboard-form.tsx:24-32` | `TenantOnboardTests.cs::Applies_The_Same_Rules_As_Platform_Create` |
| AC-135 | satisfied | `TenantAuthorizationService.cs:186,202-206; TenantOnboardEndpoint.cs:38-42` | `TenantOnboardTests.cs::Grants_No_Platform_Permission` |
| AC-136 | partial | `TenantOnboardEndpoint.cs:38-42 (no AllowAnonymous, so the pipeline challenges first); TenantRefusalResultHandler.cs:76-79 leaves challenges untouched` | `TenantOnboardTests.cs::Unauthenticated (asserts Unauthorized and that no tenant row was written; the response carries no body to assert a code on)` |
| AC-137 | satisfied | `TenantOnboardEndpoint.cs:38-42,49-54,74; TenantService.cs:190-192` | `TenantOnboardTests.cs::Existing_Member_Can_Create_Another_Tenant` |
| AC-138 | satisfied | `ClaimConstants.cs:17; Helper.cs:56-70; TenantContextProcessor.cs:57,73,100-103; no request-borne tenant in Source/` | `TenantSwitchTests.cs::Request_Supplied_Tenant_Is_Ignored, ::Session_Tenant_Governs_The_Request` |
| AC-139 | satisfied | `TenantSwitchEndpoint.cs:115; TenantAuthorizationService.cs:406-444; TokenService.cs:104-134` | `TenantSwitchTests.cs::Valid_Input, ::Switch_Applies_To_Subsequent_Requests; SessionValidationMiddlewareTenantTests.cs::Switch_Recomputes_Permissions_At_Request_Time` |
| AC-140 | satisfied | `TokenEndpoint.cs:115-128; Helper.cs:65-66; TenantContextProcessor.cs:61-91` | `TenantSwitchTests.cs::No_Active_Tenant_When_Multiple_Memberships` |
| AC-141 | satisfied | `tenant-routing.ts:7,23-27; auth-urls.ts:49-63; sidebar/index.tsx:51-55; search-component.tsx:30-34` | `tenant-routing.test.ts::treats the platform tenancy screen /admin/tenants/list as reachable with no tenant at all; auth-urls.test.ts` |
| AC-142 | satisfied | `signin-form.tsx:94-98; tenant-routing.ts:75-78; App.tsx:76-82; select-tenant-view.tsx:50-77` | `tenant-routing.test.ts::sends a caller with tenants to choose among but none chosen to the chooser; TenantSwitchTests.cs::No_Active_Tenant_When_Multiple_Memberships` |
| AC-143 | satisfied | `no-tenant-view.tsx:54-57; tenant-onboard-form.tsx:55-81` | `TenantOnboardTests.cs::Valid_Input; tenant-routing.test.ts::sends a caller who belongs to no tenant to the screen that explains it` |
| AC-144 | satisfied | `RoleConfiguration.cs:31-33 (IX_Roles_TenantId_Name, AreNullsDistinct(false), unfiltered); TenantConfiguration.cs:31-32` | `TenantUniquenessTests.cs::Uniqueness_Is_Enforced_By_The_Database_Over_Retained_Rows, ::Platform_Role_Names_Are_Unique (insert past the service layer, assert the constraint name)` |
| AC-145 | satisfied | `DataSeeder.cs:60-62,146,178,245` | `TenantSeedingTests.cs::No_Public_Role_Is_Seeded, ::Every_Role_Has_A_Declared_Scope` |
| AC-146 | satisfied | `TenantService.cs:174-190; TenantAuthorizationService.cs:186,202-206` | `TenantOnboardTests.cs::Creator_Is_Administrator_Only_Of_The_Tenant_Created, ::Grants_No_Platform_Permission` |
| AC-147 | satisfied | `Unfiltered unique indexes over retained rows (TenantConfiguration.cs:31-32, RoleConfiguration.cs:31-33)` | `TenantUniquenessTests.cs::Deleted_Names_Are_Refused_As_Duplicates, ::Soft_Deleted_Identifier_Is_Still_Reserved, ::Deleted_Role_Name_Is_Still_Reserved` |
| AC-148 | satisfied | `TenantContextProcessor.cs:57,73; SessionValidationMiddleware.cs:39-50` | `TenantSwitchTests.cs::Session_Tenant_Governs_The_Request; SessionValidationMiddlewareTenantTests.cs::Membership_Revocation_Takes_Effect_On_Next_Request` |
| AC-149 | satisfied | `TenantSwitchEndpoint.cs; SessionValidator.cs:90-98` | `TenantSwitchTests.cs::No_Active_Tenant_When_Multiple_Memberships, ::Selection_Grants_Exactly_That_Tenants_Data` |

## Criteria that are not satisfied

Fourteen. Each entry gives the code, the tests, the gap, and the task that closes it.

### AC-045 — partial

- **Criterion:** a caller without the permission is refused with the standard forbidden response
  *and a defined error code*.
- **Code:** `Middleware/TenantRefusalResultHandler.cs:98` returns `null` when the tenant status is
  `Active`, deferring to the framework default. It is the only
  `IAuthorizationMiddlewareResultHandler` in the backend.
- **Tests:** `TenantPermissionTests.cs::Missing_Permission_Is_Forbidden` — status code only.
- **Gap:** the error-code clause is unmet. A permission refusal is the framework default handler
  with an empty 403 body and no `ErrorCodes` value; only refusals caused by the tenant *state* are
  coded. The test file itself records that the code half is unasserted
  (`TenantPermissionTests.cs:35-39`).
- **New task:** **T-148**.

### AC-136 — partial

- **Criterion:** an unauthenticated onboarding attempt is refused *and returns a defined error code*.
- **Code:** `TenantOnboardEndpoint.cs:38-42` carries no `[AllowAnonymous]`, so the pipeline
  challenges before the endpoint runs; `TenantRefusalResultHandler.cs:76-79` deliberately leaves
  challenges untouched.
- **Tests:** `TenantOnboardTests.cs::Unauthenticated` — asserts `Unauthorized` and that no tenant row
  was written.
- **Gap:** the refusal is a bare framework 401, with no response body to carry a code. The spec
  distinguishes this from AC-098 on purpose: AC-098 asks for "the standard unauthenticated response"
  for files and is satisfied, while AC-136 asks for a defined code.
- **New task:** **T-149**. Shares `TenantRefusalResultHandler.cs` and `ErrorCodes.cs` with T-148, so
  the two are sequenced rather than parallel.

### AC-075 — partial

- **Criterion:** every user-visible string this feature introduces is rendered from a translation
  key, with no hard-coded text.
- **Code:** `app/[lang]/admin/(tenancy)/tenants/list/_components/tenant-table.tsx:129-134` builds
  export rows from the literals `Name`, `Identifier` and the sheet name `'Tenants'`; only `Status` is
  translated. `tenant-filter-panel.tsx:33,34,35,50,58,73,82` and `tenant-filter-button.tsx:32` carry
  eight `t('…') || 'English literal'` fallbacks.
- **Tests:** `i18n/locales.test.ts` covers every `t()` call and every locale, but no literal.
- **Gap:** hard-coded user-visible text in new files. The eight fallbacks are unreachable — all eight
  keys exist in all 8 locales — but they are literals in a new file, so they count. The fix is
  mechanical: `table.columns.name`, `table.columns.identifier`, `table.columns.status` and
  `page.tenants.title` already exist in all 8 locales, so only the sheet name needs a new key.
- **New task:** **T-151**.
- **Decision for you:** the export-header and fallback pattern is copied verbatim from the
  pre-existing users screens (`user-table.tsx:135-141`, `user-filter-panel.tsx`). It is a
  template-wide convention, not a tenancy defect. Fixing it only in the new screens makes the new
  screens inconsistent with the old ones; fixing it everywhere is a larger change than this feature.
  See "Where the criterion and the template disagree" below.

### AC-077 — partial

- **Criterion:** the interface is correct in RTL, with no direction-specific spacing or alignment
  assumptions.
- **Code:** logical utilities are used correctly at `tenant-switcher.tsx:115,133`,
  `select-tenant-view.tsx:110` and `tenant-table.tsx:318` — but one physical utility remains:
  `tenant-filter-button.tsx:34` uses `ml-1` on a badge, which is the wrong side in RTL.
- **Tests:** none. No RTL test exists anywhere in the repository, and `tasks.md` schedules none.
- **Gap:** one direction-specific class in a new component, and no test evidence at all.
- **New task:** **T-152** (sequenced after T-151 — same file).
- **Decision for you:** `ml-1` is copied from the pre-existing `user-filter-button.tsx:34`. Same
  template-wide framing as AC-075.

### AC-125 — partial — **the one real defect**

- **Criterion:** signing out discards the stored active-tenant selection *and* the tenant-scoped data
  cached in the browser, so the next user on that browser inherits no selection and sees no previous
  tenant's records.
- **Code:** `store/tenant-cache.ts:31-35` defines `signedOutActions()` — reset the API cache, sign
  out, clear the unread badge — which is correct and is dispatched at `nav-user.tsx:47` and
  `no-tenant-view.tsx:37`. It is **not** dispatched at
  `app/[lang]/(auth)/change-password/_components/change-password-form.tsx:57`, which calls a bare
  `dispatch(signout())` and then `router.push('/signin')`.
- **Tests:** `store/tenant-cache.test.ts` asserts the builder returns the right triplet in the right
  order — in isolation, from the builder itself. `authSlice.test.ts::signout` covers the reducer.
  `SignoutTests.cs::Signout_Clears_The_Session_Tenant` covers the server. **Both web tests stay green
  under the broken path.**
- **Gap:** a real gap, not a test gap. Changing a password is a sign-out path — the endpoint calls
  `RevokeAllAsync` and rotates the session version, and the form then leaves for `/signin`. The store
  is a module-level singleton (`store/index.tsx:11`) and `router.push` is a soft client navigation,
  so `appApi`'s cache survives onto the sign-in page; `signin-form.tsx:81-107` never resets it
  either. With the default `keepUnusedDataFor` of 60s, the next user on the same browser can be
  served the previous tenant's cached records, which the criterion expressly forbids.
  `frontend-contract.md:203-204` states the rule without qualification, and `tenant-cache.ts`'s own
  doc-comment states it too. No task line mentions `change-password`, which is how it was missed.
- **New task:** **T-150**.

### AC-030 — partial

- **Criterion:** a persisted tenant-scoped record is attributed to the active tenant *without relying
  on the caller to supply it*.
- **Code:** `Data/AppDbContext.cs:274-292` (`AttributeAddedEntry`) stamps a null `TenantId` from
  `tenantContext.CurrentTenantId` and refuses re-attribution when a non-null `TenantId` disagrees
  with the active one. The guard exists and reads correctly.
- **Tests:** `TenantFilterTests.cs::Attribution_Is_Applied_On_Save` exercises only the null-`TenantId`
  path.
- **Gap:** the re-attribution refusal is never exercised. `TenantAttributionException` is thrown at
  `AppDbContext.cs:291,308` and appears in **no file** under `src/backend/Tests`. No test supplies a
  `TenantId` differing from the active tenant, so deleting the
  `entry.Entity.TenantId != activeTenantId` check at `:287-292` would leave the whole suite green.
  Its sibling guard `TenantScopeNotEstablishedException` **is** asserted — which is why AC-080 stands
  while AC-030 does not.
- **New task:** **T-153**.

### AC-019 — partial

- **Criterion:** removing or stripping the last administrator who holds tenant-administration
  permission is refused.
- **Code:** `TenantMembershipService.cs:285,257` with `TenantAuthorizationService.cs:273-294`; the
  guard counts permission holders through `UserRoles`, not merely members — which is the right
  reading.
- **Tests:** `TenantMemberRemoveTests.cs::Cannot_Remove_Last_Administrator`,
  `TenantMemberUpdateRolesTests.cs::Cannot_Strip_Last_Administrator_Role`.
- **Gap:** the "holding tenant-administration permission" clause is unproven in the negative. Every
  refusal test arranges a tenant whose only member *is* the administrator, so a guard that counted
  "any other member" would pass them identically. No test leaves a non-administrator member in place
  while the last administrator is removed or stripped.
- **New task:** **T-154**.

### AC-039 — partial

- **Criterion:** a role name that duplicates an existing one **on creation or rename**, including
  against a deleted role, is refused.
- **Code:** `RoleUpdateEndpoint.cs:58-72` has the rename half — normalized, both query filters named,
  compensated by an explicit tenant predicate. `RoleCreateEndpoint.cs:43-53` has the create half.
- **Tests:** `RoleCreateTests.cs::Duplicate_Name_In_Same_Tenant_Ignoring_Case`,
  `TenantUniquenessTests.cs::Deleted_Role_Name_Is_Still_Reserved`.
- **Gap:** the rename half has no test anywhere. No test renames a role onto a name already in use in
  that tenant, live or deleted. `RoleNameAlreadyExists` is asserted only on creation paths
  (`RoleCreateTests.cs:138`, `TenantUniquenessTests.cs:108,258`).
- **New task:** **T-155**.

### AC-113 — partial

- **Criterion:** a platform administrator may list, read and administer roles across every tenant.
- **Code:** `RoleService.cs:130-133` widens through `AcrossAllTenants()` under platform
  administration; `RoleListEndpoint.cs:41-44` and the get/update/delete/change-permissions endpoints
  inherit it by reading `Roles()`.
- **Tests:** `RoleListTests.cs::Platform_Administrator_Sees_Roles_Across_Tenants`,
  `RoleListTenantFilterTests.cs::Platform_Administrator_Narrows_The_List_To_A_Named_Tenant`.
- **Gap:** the *read and administer* clause is unproven; only the list verb is tested. No test has a
  platform administrator get, update, delete or change the permissions of a role belonging to a
  tenant it is not a member of. Every cross-tenant single-role test uses a non-platform tenant
  administrator, and `Get_Role` / `Update_Role` / `Delete_Role` / `Change_Permissions` all run as the
  seeded administrator acting inside the bootstrap tenant it belongs to. The widening for four verbs
  is implemented but exercised for one.
- **New task:** **T-156**.

### AC-047 — partial

- **Criterion:** a caller who is not a platform administrator requesting a platform-wide operational
  surface, such as the background-job dashboard, is refused.
- **Code:** `HangfireAuthorizationFilter.cs:33-45`, wired at `Program.cs:241-243`.
- **Tests:** `HangfireAuthorizationFilterTests.cs::Dashboard_Requires_Platform_Administration` —
  five direct `IsAuthorized` calls over fabricated principals.
- **Gap:** no test requests the dashboard. Every call is a direct predicate invocation, so deleting
  the `Authorization = [new HangfireAuthorizationFilter()]` option at `Program.cs:243` leaves the
  suite green while Hangfire falls back to `LocalRequestsOnlyAuthorizationFilter`, which grants local
  requests. The filter is exhaustively covered; the surface it is attached to is not.
- **New task:** **T-157**.

### AC-066 — partial

- **Criterion:** a caller who is not a platform administrator gets back only the tenants in which
  that caller holds an **active** membership.
- **Code:** `Features/Tenancy/Core/TenantService.cs:139-144` — `AcrossAllTenants()` over memberships
  with the soft-delete filter in force, which is the right mechanism.
- **Tests:** `TenantListTests.cs::Non_Platform_Caller_Sees_Only_Own_Tenants` — one tenant joined, one
  never joined.
- **Gap:** the "active" qualifier is undiscriminated. The test would still pass if `Tenants()` stopped
  excluding soft-deleted memberships, so the list could keep showing a tenant whose membership was
  removed. No test removes a membership and then lists tenants as that account;
  `MembershipActivationTests.cs:31-64` covers a removed row only as a tenant-scoped refusal.
- **New task:** **T-158**.

### AC-060 — partial

- **Criterion:** a suspended **or deleted** tenant's files are not served but are retained.
- **Code:** `FileService.cs:276-289` returns `TenantNotFound` for a deleted tenant and
  `TenantSuspended` for a suspended one, with no content on either; `FileGetEndpoint.cs:81-82`,
  `TenantDeleteEndpoint.cs:61-65`.
- **Tests:** `TenantSuspensionTests.cs::Suspended_Tenant_Files_Are_Not_Served_But_Are_Retained`.
- **Gap:** the deleted branch is implemented but untested. No test deletes a tenant and then requests
  one of its files.
- **New task:** **T-159**.

### AC-028 — partial

- **Criterion:** when the active tenant changes, tenant-scoped data cached in the browser for the
  previous tenant is discarded *so that no record from it is displayed afterwards*.
- **Code:** `store/tenant-cache.ts:18-22` (`tenantChangedActions`), dispatched at
  `tenant-switcher.tsx:84,93`, `select-tenant-view.tsx:64,72` and `tenant-onboard-form.tsx:68,76`.
- **Tests:** `store/tenant-cache.test.ts:25-51` asserts only the action list its own builder returns.
- **Gap:** the trigger has no discriminating evidence. No test dispatches the reset from a component,
  and none asserts that no record from the previous tenant is displayed afterwards. A regression at
  any of the three dispatch sites leaves every test green. The builder itself is independently proven
  by AC-132, so what is missing is the wiring, not the contract. This is the behavioural twin of
  AC-132 and the reason AC-132 stays `satisfied` while AC-028 does not: AC-132 asks specifically for
  *a web test proving that changing the active tenant discards the cache*, and
  `test-plan.md:455` designates `tenant-cache.test.ts` as that test.
- **New task:** **T-160**.

### AC-070 — partial

- **Criterion:** when the active tenant becomes unavailable, the user is told why and offered another
  tenant, *instead of being signed out*.
- **Code:** `store/middlewares/rtk-error-middleware.ts:11-27,35-50,71-74`, `App.tsx:62-71`,
  `select-tenant-view.tsx:21-27,89-93,103-128`.
- **Tests:** `authSlice.test.ts` (the caller stays authenticated in the tenant they were acting in),
  `api-error-helpers.test.ts`, `TenantSuspensionTests.cs:142-143`.
- **Gap:** the trigger path is proven only piecewise. `rtk-error-middleware.ts` has **no test
  anywhere** — no test dispatches a rejected action through the store — and the `reasonKeys` mapping
  and reason banner in `select-tenant-view.tsx` are untested. A regression in the middleware's code
  list or its 403 gate would leave every other test green while the user is sent to the chooser with
  no explanation. The "never signs out" half is tested.
- **New task:** **T-161**.

## Spec drift — code no criterion asked for

Surfaced for your decision. None of these is a criterion failure; each is either unplanned code or a
planned rule that the implementation deviates from.

### U1 — a second tenant-refusal seam, planned by nobody

`Middleware/TenantRefusalResultHandler.cs` and `Tenancy/TenantRefusal.cs`, registered at
`Program.cs:88`.

`plan.md:98-99` (decisions D9 and D10) and `api-contract.md:200-206` name `TenantContextProcessor`
as the single enforcement point. Neither file appears in `spec.md`, `plan.md`, the contract
documents, or — verified by string search — in **any task line** in `tasks.md`.

The underlying hole is real: a stale tenant selection leaves the caller holding zero permissions, so
endpoint authorization refuses *before* the pre-processor runs, and without this handler the caller
would get an uncoded 403 and never be offered another tenant (AC-070, AC-029). So this is unplanned
code fixing a real gap rather than an unplanned feature. Either the plan should be amended to record
it as the enforcement point for refusals that authorize ahead of the processor, or the refusal should
be moved into the processor's path.

### U2 — an unrequested behaviour change in the shared paging validator

`Base/Dto/ListRequestDtoValidator.cs:15-19` changed the paging predicate from
`x.IncludeIds?.Count == 0` to `request.IncludeIds is null or { Count: 0 }`.

That is a behaviour change, not a refactor. Previously a null `IncludeIds` skipped `Page` and
`PageSize` validation entirely; now every list endpoint in the application inherits those rules,
including endpoints this feature does not touch. No criterion covers it. It is very likely the right
change — the old behaviour was a hole — but it is a template-wide change made inside a feature, and
the template is what ships to new projects.

### C1 — the role rename breaks the stated query-filter rule

`RoleUpdateEndpoint.cs:26` declares `private const string TenantFilterKey = "Tenant";` and `:63`
calls `.IgnoreQueryFilters([TenantFilterKey, SoftDeleteFilterKey])`. `plan.md:345` says: "No raw
`IgnoreQueryFilters(["Tenant"])` call sites — `AcrossAllTenants()` is the only opt-out."

The rule cannot be kept as written: the endpoint needs both filters off at once and the helper
relaxes one. It compensates with an explicit `role.TenantId == tenantId` predicate, so the behaviour
is correct and the tests pass — the mechanism rule is what is broken. Either the helper needs a
two-filter form or `plan.md:345` needs to admit this site.

### D1 — `ITenantContext.BeginUnscoped` is unreachable in production

Specified by decision D6, so not drift — but it has no production call site. Only tests and the
design-time no-op factory call it, which also means the AC-080 advisory below describes code that
cannot be reached.

### D2 — `GetUserInfoResponse.isPlatformAdministrator` is never read

Delivered to the web client and referenced only by test fixtures. Either the client should use it
(`auth-urls.ts` and `App.tsx` derive platform-ness from permissions instead) or the field is dead
weight on the wire.

### Checked and clear

No `AuthorizationStamp` type. No `IsPlatform` column on `Permission`. No `TenantId` on `UserRole`.
No second marker interface. No `TenantSetStatusEndpoint`. No browser storage for the active tenant.
`IStorageProvider` / `LocalStorageProvider.GetSafePath` unchanged. The notification
`ON CONFLICT ("NotificationId","UserId") DO NOTHING` statement is not rewritten — only a `TenantId`
predicate was added. No new npm dependency. The 13 contracted endpoints, 12 error codes and 12
`Allow` constants all match the contract documents, and every added translation key is a contract
key.

## Advisories — recorded, not downgraded

These are notes about evidence quality, not criterion failures.

- **AC-080** stands on its own wording — the clause is a row persisted *without* attribution, which
  is refused and asserted. Its neighbour, a row carrying a caller-supplied `TenantId` that disagrees
  with the active one, is AC-030's clause and is untested. Under `BeginUnscoped()` an added row
  naming any tenant is accepted unchecked, but that method has no production call site (D1).
- **AC-085** — the generator-exclusion clause has no automated test. `tool/EasyForNetTool.Tests/`
  contains only `NamespaceRewriterTests.cs`; no test scaffolds a project and asserts its Initial
  migration. The schema half of the criterion is tested against a first-time creation.
- **AC-090** — three tests read the seeded fixture rather than creating their own rows. Read-only, no
  writes, parallel-safe. Whole-suite parallel safety is a review item by design
  (`test-plan.md:516`).
- **AC-132** — the assertion is structural (the action list and its ordering) rather than a render
  assertion that a previous tenant's record is absent from the DOM. That is the repository's intended
  standard for this criterion; there is no DOM test environment, by design.
- **AC-086** — four test names in `test-plan.md` drifted from the code; the behaviours exist under
  other names. The plan is not wrong about what is covered.

## Where the criterion and the template disagree

Three criteria — AC-045, AC-075 and AC-077 — fail because the feature followed an existing
convention of this template rather than because the tenancy work was careless:

- AC-045 asks for a defined error code on a permission refusal. No permission-gated endpoint
  anywhere in the template emits one today; `TenantPermissionTests.cs:35-39` says so outright. Adding
  a code for tenancy endpoints alone would make them the only coded 403s in the application.
- AC-075 asks for no hard-coded strings. The new tenant screens copy the export headers and the
  `t('…') || 'literal'` fallback shape from the pre-existing user screens verbatim.
- AC-077 asks for no direction-specific assumptions. The one `ml-1` is copied from the pre-existing
  user filter button.

Each is fixable in the new screens alone (small, ~1 file per criterion) or across the template
(larger, and it changes what ships to new projects). **`spec.md` has not been edited** — per the
verify contract, a criterion is not amended to make it pass. These three are decisions for you:
change the code to match the criterion, or amend the criterion to record the template convention.

## Convergence

**Not converged.** 135 satisfied, 14 partial, 0 missing. Round 13 of `tasks.md` holds the remaining
work as `T-148`..`T-161`; the Coverage table has been updated so each of the 14 criteria names its
new task.

Suggested next step: `/implement 001-multi-tenant-system --only T-148,T-149,T-150,T-151,T-152,T-153,T-154,T-155,T-156,T-157,T-158,T-159,T-160,T-161`,
then `/verify 001-multi-tenant-system` again. Thirteen of the fourteen are test work over behaviour
that already exists and is correct; **T-150 is the one that changes production behaviour**, and it is
the only one that affects a user today.

## What this run did not do

- It did not edit `spec.md`, or any criterion, plan, contract or task text.
- It did not change any source file. All 22 subagents were launched read-only and instructed to
  report only.
- It did not treat a passing test suite as evidence of a criterion. A test that passes under a broken
  implementation is the finding, not the proof.
