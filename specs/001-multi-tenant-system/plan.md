# Implementation plan - Multi-tenant system

Spec: `specs/001-multi-tenant-system/spec.md` (149 acceptance criteria, AC-001..AC-149).

## Chosen approach

**Reuse-first, with the isolation kernel lifted out of the feature tree.**

Tenancy is carried through the mechanisms this repository already has, not through parallel
copies of them:

- one **named EF Core 10 query filter** registered beside the existing soft-delete filter in
  `src/backend/Source/Data/AppDbContext.cs`, so tenancy and soft delete coexist and either can be
  relaxed alone (AC-034, AC-036);
- one **single nullable marker** `ITenantScoped { Guid? TenantId }` and one filter expression
  `e.TenantId == CurrentTenantId`, so a new persisted kind is scoped by implementing one interface
  and nothing else;
- the active tenant travelling as a **claim inside the existing claim/session pipeline**
  (`src/backend/Source/Helper.cs`, `src/backend/Source/Middleware/SessionValidationMiddleware.cs`),
  never as a request value or route segment (AC-138);
- **existing endpoints, RTK Query slices and translation namespaces gaining tenant predicates
  rather than duplicates** — `UserListEndpoint`, `RoleListEndpoint`, `NotificationListEndpoint`,
  `store/api/_app-api.ts` and `public/locales/*.json` all keep their routes, payload shapes and key
  namespaces, and change only the rows they return (AC-093..AC-096, AC-110..AC-113).

Two structural corrections to that framing, made because the repository's own rules make them free:

1. **The isolation kernel lives in root namespaces**, not inside a feature.
   `Backend.Data.Entities` gets `Tenant` and `TenantMembership`; `Backend.Tenancy` gets
   `ITenantContext`; `Backend.Attributes` gets `[AllowNoTenant]`; `Backend.Processors` gets the
   pre-processor; `Backend.Extensions` gets `AcrossAllTenants()`.
   `src/backend/Tests/Architect/FeatureDependencyTester.cs` scans only types under
   `Backend.Features`, so root-namespace kernel types cost **zero** `[AllowOutside]` marks.
   `src/backend/Source/Data/Entities/Base/` exists for exactly this purpose already, and
   `AppDbContext`, `DataSeeder`, `AppDbContextFactory`, the Hangfire jobs and the pre-processor all
   consume the kernel — a feature namespace would be the wrong home for every one of them.

2. **Tenant lifecycle is its own vertical slice**, `src/backend/Source/Features/Tenancy/`, with the
   shape every other capability here follows: `TenancyFeature.cs`,
   `Core/TenancyPermissionsProvider.cs`, `Core/TenantService.cs`, `Core/TenantMembershipService.cs`,
   `Endpoints/Tenants/TenantsGroup.cs`, and `src/backend/Tests/Features/Tenancy/`.
   Thirteen endpoints, two entities, three permission parents and two services do not belong bolted
   onto `Features/Identity`, which is already the largest slice in the repository.

### Why this beat the alternatives

| Alternative | Why it lost |
| --- | --- |
| **Tenancy folded into `Features/Identity`** (the framing as originally proposed) | Its only stated justification was avoiding `[AllowOutside]` marks, and that justification does not hold: `FeatureDependencyTester` scans `Backend.Features.*` only, so root-namespace entities need no marks at all. Folding also leaves tenancy with no `<Feature>Feature.cs`, no `<Feature>PermissionsProvider.cs` and no `Tests/Features/Tenancy/` — the shape `CLAUDE.md` and the `backend-feature` skill both describe — and permanently fuses the isolation kernel, the tenant lifecycle and the user/role graph into one slice. |
| **A naive `Features/Tenancy` slice with navigation properties to `User` and `Role`** | Would force `[AllowOutside]` on `User`, `Role` and `UserRole` and make three Identity entity types part of a public cross-feature surface. Avoided here with bare `Guid` foreign keys and no navigation properties on `TenantMembership`. |
| **A per-query `.Where(x => x.TenantId == …)` convention** | AC-091 requires an automated check that a *new* persisted kind cannot be added unscoped. A convention is provable only by review. |
| **Fusing the tenant predicate into the existing unnamed soft-delete filter** | An opt-out would then also disable soft-delete exclusion, directly violating AC-034 and AC-036. |
| **A tenant id header or a `/{tenant}/` route segment** | Refused by AC-138 and by the spec's "Tenant addressing" note: the locale-prefixed route tree keeps its shape. |
| **A `TenantMembershipRole` join table replacing `UserRole` inside tenants** | Duplicates `UserRole`, orphans the role-assignment code paths in `UserCreateEndpoint`/`UserUpdateEndpoint`, and doubles the surface AC-081 must keep transactional. |
| **A per-membership `AuthorizationStamp` for authorization freshness** | Rejected outright — see decision D8. It distributes correctness across every present and future write path, and a missed bump is invisible to the architecture test. |
| **An `IsPlatform` column on the `Permission` entity** | Avoidable schema churn. The catalogue is code-declared and reconciled on every start, so scope is derivable in memory. |
| **Separate tenant-scoped user and role endpoints beside the existing platform ones** | Doubles five user endpoints, six role endpoints, eleven RTK Query endpoints and five screens for a difference the criteria (AC-093..AC-096, AC-110..AC-113) describe as a row filter. |

### Ideas adopted from the runner-up approaches

- **`AcrossAllTenants()`** (clean-slice) — every legitimate cross-tenant query goes through one
  named extension in `src/backend/Source/Extensions/TenantQueryExtension.cs` rather than scattered
  `IgnoreQueryFilters(["Tenant"])` call sites. AC-036's "explicitly and by name" becomes one grep
  and one place to review.
- **`AuthToken.TenantId`** (clean-slice) — the active tenant is persisted on the refresh-token row,
  because `TokenService.SetRenewalPrivilegesAsync`
  (`src/backend/Source/Features/Identity/Endpoints/Account/TokenService.cs`) has only a `UserId` to
  rebuild claims from and would otherwise silently drop or carry the wrong tenant across a refresh.
- **A single nullable `ITenantScoped` marker plus an explicit `BeginPlatformScope()`**
  (clean-slice) — one filter registration, one `SaveChanges` branch, one architectural check, and no
  hand-maintained per-entity filter expressions inside `AppDbContext`.
- **Enforcement by global pre-processor with `[AllowNoTenant]`** (clean-slice) rather than
  middleware, because only FastEndpoints metadata knows which routes are exempt.
- **Prove the kernel in wave 1** (risk-first) — an EF Core 10 named-filter spike lands and passes
  before any endpoint is written, together with the AC-091 model-walk architecture test.
- **An ordered, backfilled migration with a deterministic bootstrap-tenant Guid** (risk-first) —
  create, seed, add nullable, backfill, tighten, re-index; no startup heuristic attributes
  pre-existing rows.
- **Enumerate every raw-SQL / `ExecuteUpdate` / `ExecuteDelete` site by name with a test each**
  (risk-first), and choose index database names whose last underscore segment is the offending
  *request field*, since `ExceptionProcessor.GetErrorMessage` derives the reported field with
  `constraintName.Split('_').Last().ToLowerInvariant()`.

---

## Architecture decisions

| Decision | Rationale | Rejected alternative |
| --- | --- | --- |
| **D1. Kernel in root namespaces.** `Tenant` and `TenantMembership` in `src/backend/Source/Data/Entities/`; `ITenantScoped` in `src/backend/Source/Data/Entities/Base/ITenantScoped.cs`; `ITenantContext`/`TenantContext` in `src/backend/Source/Tenancy/`; `AcrossAllTenants()` in `src/backend/Source/Extensions/TenantQueryExtension.cs`; `[AllowNoTenant]` in `src/backend/Source/Attributes/`; `TenantContextProcessor` in `src/backend/Source/Processors/`. | `FeatureDependencyTester` scans only `Backend.Features.*`, so root-namespace types cost zero `[AllowOutside]`. `AppDbContext`, `DataSeeder`, `AppDbContextFactory`, Hangfire jobs and the pre-processor are all non-feature consumers — the `backend-feature` skill's own first preference is "move it to a shared root namespace". | Kernel types under `Features/Identity/Core` or `Features/Tenancy/Core`: makes every non-feature consumer a cross-feature reference and forces marks on the accessor as well as the marker. |
| **D2. Tenant lifecycle is a real slice** at `src/backend/Source/Features/Tenancy/` with `TenancyFeature.cs`, `Core/TenancyPermissionsProvider.cs` (group name `Tenancy`), two services, one endpoint area, and `src/backend/Tests/Features/Tenancy/`. | Thirteen endpoints, three permission parents and two services are a capability, and `CLAUDE.md` says a capability is a vertical slice. Keeps `Features/Identity` from becoming the repository's centre of gravity. | Folding it into `Features/Identity`: see the table above. |
| **D3. Exactly one cross-feature contract.** `Features/Tenancy` depends on `Features/Identity` through one new `[AllowOutside] ITenantAuthorizationService` in `src/backend/Source/Features/Identity/Core/TenantAuthorizationService.cs`, whose signatures use only `Guid`, `string` and its own DTOs — never `User`, `Role` or `UserRole`. It owns: does this account exist; provision a tenant's system-created administrator role; replace a member's role assignments in a tenant; does any other member of this tenant hold tenant administration. **`Features/Identity` depends on nothing in `Features/Tenancy`** — it reads `dbContext.TenantMemberships` (root namespace) directly. | One `[AllowOutside]` mark, one direction, no entity types crossing the boundary, and `TenantMembership` carries a bare `Guid UserId` with no `User` navigation. The role graph stays owned by the slice that owns roles. | Marking `IUserService`, `IRoleService`, `User`, `Role` and `UserRole` `[AllowOutside]` (five marks, three entity types exposed); or a bidirectional pair of contracts, which is a dependency cycle at slice level. |
| **D4. One named filter, one marker, one expression.** `SoftDeleteFilter` is changed to register the *named* filter `"SoftDelete"`, and a new `TenantFilter(modelBuilder)` registers `"Tenant"` as `e.TenantId == CurrentTenantId` for every `ITenantScoped` type, reusing the same model-walk loop. `AppDbContext` exposes `public Guid? CurrentTenantId => tenantContext?.CurrentTenantId;` so the filter re-evaluates per query rather than baking a captured value into the model. | EF Core 10 (Npgsql 10.0.0, `net10.0`) supports several named filters per entity type and `IgnoreQueryFilters(["Tenant"])`. One expression means a new entity author implements one interface and edits nothing in `AppDbContext`. | Two markers (`ITenantScoped` strict + `ITenantOwned` nullable) with three hand-written per-entity expressions inside `AppDbContext.TenantFilter` — a hand-maintained list a new entity author has to find and edit. |
| **D5. `TenantId == null` means platform scope, and the three "visible everywhere" cases are explicit call sites, not filter branches.** Platform roles (`Role.TenantId == null`, AC-110/AC-121), platform-wide notifications (`Notification.TenantId == null`, AC-054) and account-owned files (`StoredFile.TenantId == null` with `OwnerUserId` set, AC-097) are each reached by a narrowed `AcrossAllTenants().Where(x => x.TenantId == null && …)` in the endpoint that needs them. | Keeps D4's single expression. `null` genuinely means the opposite thing on `Role` (invisible inside a tenant) and on `Notification` (visible in every tenant), and that difference belongs in the three queries that express it, where it is greppable, rather than in the model builder. | A second nullable marker with per-entity filter expressions; or a uniform `null || current` filter, which would leak the platform role into every tenant's role list (AC-110). |
| **D6. Explicit tenant scope for non-request work.** `ITenantContext` exposes `CurrentTenantId`, `IsResolved`, `BeginTenant(Guid)`, `BeginPlatformScope()` and `BeginUnscoped()`, each returning an `IDisposable`. Outside an HTTP request the context is **unresolved**, which is a distinct state from platform scope: `SaveChanges` rejects an `ITenantScoped` insert made while unresolved (AC-080), so a background job that forgets `BeginTenant` fails loudly rather than writing an unattributed row or silently reading none. | AC-035, AC-037 and AC-131 all require background work to *fail* without an explicit tenant, not to silently see nothing. `ICurrentUserService` is the precedent for a root-consumed ambient service, and its HTTP-only limitation is exactly why a second accessor is needed. | Reading the tenant claim through `ICurrentUserService` with no second service: Hangfire jobs, `DataSeeder` and `AppDbContextFactory` have no HTTP context and could not set one. |
| **D7. Design-time safety, decided rather than deferred.** `AppDbContext` reads the tenant through `tenantContext?.CurrentTenantId` (null-conditional), and `src/backend/Source/Data/AppDbContextFactory.cs` stops passing `null!`: it passes design-time no-op `ICurrentUserService` and `ITenantContext` implementations declared in that same file. | `dotnet ef migrations add Initial` is literally the first command a newly generated project runs (AC-085, the `new-project` skill). A null dereference during model building would break scaffolding for every generated project. | Leaving `null!` in place and hoping the filter expression never dereferences it. |
| **D8. Authorization freshness by per-request recomputation, inside the middleware that already runs one query per request.** `SessionValidationMiddleware` is extended (not duplicated) so that a single query keyed on `(userId, tenantId)` returns the password hash, the tenant's lifecycle status, whether the membership is live, and the role and permission names the member holds *in that tenant*. On success it **replaces** the principal's `ClaimTypes.Role` and `ClaimConstants.Permission` claims with that freshly computed set, so `UseAuthorization()` evaluates `Permissions(Allow.X)` against current data. | AC-108, AC-116, AC-117 and AC-127 are then satisfied by construction, at every present and future write path, with nothing to remember. This is also the only point in the pipeline where it can work: FastEndpoints' `Permissions()` is an ASP.NET authorization policy evaluated by `UseAuthorization()`, *before* pre-processors run, and this middleware already sits between `UseAuthentication()` and `UseAuthorization()` in `src/backend/Source/Program.cs`. Per-request cost is unchanged — the existing session query and the new tenant query are merged into one. | A `TenantMembership.AuthorizationStamp` compared in `SessionValidator`: it only reports that claims are stale, it distributes a required "bump" across every write path that changes a membership, an assignment, a role's permissions or a role's existence, and the architecture test cannot catch a missed one. |
| **D9. A failed tenant check refuses the request; it never signs the user out.** The middleware's only current behaviour on a negative result — clear the principal, delete the `refreshToken` cookie, `SignOutAsync` — is retained **solely** for the password-hash session-version check. A tenant problem instead records a typed reason on `HttpContext.Items` and leaves the user authenticated; `TenantContextProcessor` turns that into a 403 carrying `tenantSuspended`, `tenantMembershipRevoked`, `tenantNotFound` or `noActiveTenant`, and the web app offers another tenant. | AC-020, AC-029 and AC-070 explicitly forbid the silent sign-out and require an explanatory refusal plus an offer to choose another tenant. Saying this out loud is the point: "the claims are stale" and "what the caller sees" are two different questions, and only the second is what the spec measures. | Reusing the existing sign-out path for tenant failures — precisely the behaviour the spec forbids. |
| **D10. One enforcement point: `TenantContextProcessor`,** a global pre-processor wired into the existing `c.Endpoints.Configurator` in `src/backend/Source/Program.cs` beside `ToLargePayloadProcessor`. It requires an established, existing, unsuspended tenant with a live membership, unless the endpoint type carries `[AllowNoTenant]`. Exempt: sign-in, sign-up, refresh, verify-email, forgot/reset/change password, profile, `get-info`, `TenantListEndpoint`, `TenantSwitchEndpoint`, `TenantOnboardEndpoint`, and every platform-tier endpoint. | AC-007, AC-022, AC-023, AC-026, AC-029, AC-037, AC-050 and AC-099 are one check that would otherwise be copy-pasted into every endpoint. `Program.cs` already attaches three global processors this way, so the wiring is a line, not a mechanism. Exempting by attribute keeps AC-051's self-service flows and AC-133's onboarding usable with no tenant. | A per-endpoint guard in each `HandleAsync` (unenforceable), or an ASP.NET middleware before `UseFastEndpoints` (cannot see FastEndpoints endpoint metadata to know which routes are exempt). |
| **D11. Roles become tenant-scoped by adding `Role.TenantId` (`Guid?`) and `ISoftDelete` to the existing entity,** and changing `RoleConfiguration`'s unique index from `NameNormalized` to the composite `(TenantId, NameNormalized)` **with `NULLS NOT DISTINCT`** (`.IsUnique().AreNullsDistinct(false)`), database-named `IX_Roles_TenantId_Name`. `Tenant` gets `ISoftDelete` and `IHasNormalizedProperties` with a unique index on `IdentifierNormalized`, database-named `IX_Tenants_Identifier`. | AC-039/AC-144/AC-147 require per-tenant uniqueness that survives deletion, and roles are hard-deleted today (`RoleService.DeleteAsync` calls `Remove`), so a deleted name would be reusable. Without `NULLS NOT DISTINCT`, PostgreSQL treats every `NULL` tenant as distinct and **platform role names would not be unique at all**. The explicit database names matter because `ExceptionProcessor` reports `constraintName.Split('_').Last()`: `IX_Roles_TenantId_Name` yields `name`, matching the request field, where the default `IX_Roles_TenantId_NameNormalized` would yield `namenormalized`, which no web form knows. | A filtered unique index over live rows only (refused by AC-144 and the "Query cost" note); default EF index names (produce a field name the web side cannot map). |
| **D12. A member's roles inside a tenant are the existing `UserRole` rows whose `Role.TenantId` equals that tenant.** `TenantMembership` carries only `Id`, `TenantId`, `UserId`, audit columns, soft-delete columns and an `xmin` concurrency token. | Maximum reuse of the existing junction and of `IUserService.GetUserRolesAsync`/`GetUserPermissionsAsync`, which need only a tenant predicate. A membership row is still required for AC-018 (revoke while leaving the account intact), AC-078, AC-079, AC-103, and for a member holding zero roles. | A `TenantMembershipRole` table: duplicates `UserRole` and orphans the existing role-assignment paths in `UserCreateEndpoint`/`UserUpdateEndpoint`. |
| **D13. `UserRole` is deliberately *not* `ITenantScoped`;** its tenant is derived through `Role.TenantId`. The AC-091 architecture check's exemption list is a `Dictionary<Type, string>` of **written reasons**, so an exemption can never be added silently. | Adding `TenantId` to `UserRole` would duplicate a fact already stored on `Role` and admit rows where the two disagree. Requiring a reason string turns the allowlist from a loophole into documentation, which is what makes the check fail usefully on a *new* persisted kind. | A bare `Type[]` allowlist, where a new entity can be exempted by a one-word edit nobody reviews — and where omitting `UserRole` entirely, as an earlier draft did, makes the check fail on day one. |
| **D14. Concurrency on role-assignment replacement is a PostgreSQL `xmin` token** on `TenantMembership` (`builder.UseXminAsConcurrencyToken()`). `TenantMemberUpdateRolesEndpoint` deletes and re-inserts the member's `UserRole` rows for the active tenant inside one `BeginTransactionAsync`, touching the membership row so the second concurrent writer fails with `DbUpdateConcurrencyException`, mapped to `ErrorCodes.ConcurrentModification`. | AC-081 requires one complete set of assignments, never a mixture. A transaction alone serialises the writes but still lets the later one silently overwrite a set its caller never saw; the token makes the loser fail visibly. | A transaction with no token (AC-081 then holds only by luck of interleaving); table-level locking (needless contention). |
| **D15. Suspend and reactivate are two endpoints,** `TenantSuspendEndpoint` (`Allow.Tenant_Suspend`) and `TenantReactivateEndpoint` (`Allow.Tenant_Reactivate`). | AC-071 gates each tenant operation by the permission that operation requires. A merged `TenantSetStatusEndpoint` would make that gating a branch inside a handler — invisible to `Permissions(...)`, untestable as a permission, and unusable by the web client's `isAllowed` check. | One `TenantSetStatusEndpoint` taking a target status. |
| **D16. Permission scope is derived from the code-declared catalogue, with no schema change.** `PermissionDefinition` and `FlattenedPermission` gain an **in-memory** `IsPlatform` flag set through an `AddPermission(name, displayName, isPlatform: true)` / `AddChild(...)` overload; `IPermissionDefinitionService` gains `GetPlatformPermissionNames()`. `GetDefinePermissionsEndpoint` filters by the caller's tier (AC-114/AC-115) and `ChangePermissionsEndpoint` refuses granting a platform permission to a tenant role (AC-041). The `Permission` entity, `DataSeeder`'s reconciliation loop and the migration are **untouched** by this. | AC-040 keeps the catalogue global, code-declared and reconciled from code on every start, so a permission's scope is always recomputable — persisting it would be a denormalised copy a rename could desynchronise, for no query benefit. | An `IsPlatform` column on `Permission`, plus a `DataSeeder` reconciliation branch and a migration: schema churn for a derivable fact. |
| **D17. Files gain a persisted `StoredFile` entity** under `src/backend/Source/Features/FileManagement/Core/Entities/`; `IStorageProvider` and `LocalStorageProvider.GetSafePath` are untouched. `FileUploadEndpoint` and `FileGetEndpoint` stop being anonymously reachable. | AC-058/AC-067 require a distinct `crossTenantFileAccess` code, which needs the system to distinguish "belongs to another tenant" from "does not exist" — only a row can do that. It also delivers AC-060 and AC-097 without touching the storage provider or the `ImagePreview` contract, since `User.Image` keeps storing the same generated name. | Prefixing the physical storage path with the tenant id: needs no table, but a cross-tenant request becomes indistinguishable from a miss, so AC-058's defined code cannot be produced, and `LocalStorageProviderTests`' bare-file-name invariant would have to be relaxed. |
| **D18. Raw-SQL sites are enumerated by name and individually tested.** No query filter reaches them. Today the complete set is two statements in `src/backend/Source/Features/Notifications/Endpoints/Notifications/NotificationMarkAllAsReadEndpoint.cs`: the `ExecuteUpdateAsync` over user-targeted rows, and the `ExecuteSqlInterpolatedAsync` `INSERT … ON CONFLICT` into `NotificationVisits`. Both get explicit hand-written tenant predicates. | AC-033, AC-055 and AC-130 name this statement. Enumerating the set makes it finite and reviewable; the architecture test cannot see inside a SQL string, so a written list plus a test each is the only honest coverage. | Rewriting mark-all-as-read as an EF `ExecuteUpdate` so the filter applies automatically: the `ON CONFLICT` insert for platform-wide notifications has no EF equivalent, and rewriting it risks the correctness `NotificationMarkAllAsReadTests` already pins down. |
| **D19. No new web API client and no new Redux slice.** Tenant endpoints inject into the existing `appApi`; `/account/get-info` is extended to return the caller's tenants and active tenant; `authSlice` gains `activeTenant` and `tenants`; switching and signing out call `dispatch(appApi.util.resetApiState())`. | `App.tsx` already fetches `get-info` on every load and `SigninForm` already calls it after authenticating, so AC-024, AC-073, AC-123, AC-127 and AC-142 need no extra round trip. Because the tenant lives in the session and is re-read from `get-info`, AC-124 and AC-125 are satisfied with **no browser storage at all**, and `resetApiState` is the existing RTK Query answer to AC-028/AC-132. | Persisting the active tenant in `localStorage` with a dedicated `tenantSlice`: a second source of truth the server must validate anyway (AC-127), and a stale selection left behind for the next user on the same browser. |
| **D20. The Hangfire dashboard is gated on `Allow.Platform_Administration`,** not `IsInRole("Admin")`. | AC-047 needs the dashboard on the platform tier. Once role names are per-tenant (AC-038), *any* tenant may define a role named `Admin`, so the existing check in `src/backend/Source/HangfireAuthorizationFilter.cs` becomes trivially satisfiable from inside a tenant. | Leaving the role-name check in place. |

---

## Workstreams

### 1. Kernel — governing skills: `backend-entity`, `backend-feature`, `coding-conventions`

| File | Change |
| --- | --- |
| `src/backend/Source/Data/Entities/Base/ITenantScoped.cs` | **New.** Single marker, `Guid? TenantId { get; set; }`. |
| `src/backend/Source/Data/Entities/Tenant.cs` | **New.** `AuditableEntity<Guid>`, `ISoftDelete`, `IHasNormalizedProperties`; `SystemCreated`, `Name`, `Identifier`, `IdentifierNormalized`, `TenantStatus Status`. Root namespace `Backend.Data.Entities` (D1). |
| `src/backend/Source/Data/Entities/TenantMembership.cs` | **New.** `AuditableEntity<Guid>`, `ISoftDelete`, `ITenantScoped`; bare `Guid UserId` with **no** `User` navigation (D3); `xmin` token (D14). |
| `src/backend/Source/Data/Entities/Configuration/TenantConfiguration.cs` | **New.** Table `tenancy.Tenants`; unique index on `IdentifierNormalized`, database-named `IX_Tenants_Identifier` (D11). |
| `src/backend/Source/Data/Entities/Configuration/TenantMembershipConfiguration.cs` | **New.** Table `tenancy.TenantMemberships`; composite index `(TenantId, UserId)`; `UseXminAsConcurrencyToken()`. |
| `src/backend/Source/Data/AppDbContext.cs` | Name the existing soft-delete filter `"SoftDelete"`; add `TenantFilter` registering `"Tenant"` through the same model-walk loop; add `CurrentTenantId` (D4, D7); add `DbSet<Tenant>` / `DbSet<TenantMembership>`; extend the existing `SaveChanges`/`SaveChangesAsync` audit pass with tenant stamping (AC-030) and the unattributed-write guard (AC-080). |
| `src/backend/Source/Data/AppDbContextFactory.cs` | Replace `null!` with design-time no-op `ICurrentUserService` / `ITenantContext` (D7). |
| `src/backend/Source/Tenancy/ITenantContext.cs`, `TenantContext.cs` | **New.** Root namespace, so no `[AllowOutside]` is needed; `[NoDirectUse]` on the implementation, mirroring `CurrentUserService`. `CurrentTenantId`, `IsResolved`, `BeginTenant`, `BeginPlatformScope`, `BeginUnscoped` (D6). |
| `src/backend/Source/Tenancy/TenancyConstants.cs` | **New.** `BootstrapTenantId` as a compile-time `Guid` constant, shared by the migration and `DataSeeder`. |
| `src/backend/Source/Extensions/TenantQueryExtension.cs` | **New.** `AcrossAllTenants<T>(this IQueryable<T>)` wrapping `IgnoreQueryFilters(["Tenant"])`. The only sanctioned opt-out (AC-036). |
| `src/backend/Source/Attributes/AllowNoTenantAttribute.cs` | **New.** Read by the pre-processor from the endpoint definition's type. |
| `src/backend/Source/Processors/TenantContextProcessor.cs` | **New.** Single enforcement point (D10). |
| `src/backend/Source/Middleware/SessionValidationMiddleware.cs`, `src/backend/Source/Features/Identity/Core/SessionValidator.cs` | Extended into the one-query session-plus-tenant check and the claim replacement (D8, D9). The password-hash sign-out path is unchanged. |
| `src/backend/Source/Helper.cs`, `src/backend/Source/Features/Identity/Core/ClaimConstants.cs` | `ClaimConstants.TenantId = "tenant_id"`; `CreateClaims` gains an optional tenant. |
| `src/backend/Source/Program.cs` | Register `ITenantContext`; add `TenantContextProcessor` to `c.Endpoints.Configurator`. |
| `src/backend/Source/Meta.cs` | Add `global using Backend.Tenancy;`. |
| `src/backend/Source/HangfireAuthorizationFilter.cs` | Permission claim instead of role name (D20). |

### 2. Tenancy feature — governing skills: `backend-feature`, `backend-endpoint`

`src/backend/Source/Features/Tenancy/`:

- `TenancyFeature.cs` (`IFeature.AddServices`, `[BypassNoDirectUse]`).
- `Core/TenancyPermissionsProvider.cs` — group `Tenancy`; parents `Tenants`, `TenantMembers`,
  `Platform`.
- `Core/TenantService.cs` (`ITenantService`) — the create path shared verbatim by the platform
  endpoint and self-service onboarding (AC-134).
- `Core/TenantMembershipService.cs` (`ITenantMembershipService`, `[AllowOutside]` so
  `Features/Identity` may read membership for AC-093/AC-096, D3) — owns the last-administrator
  guard (AC-019).
- `Endpoints/Tenants/`: `TenantsGroup.cs` (prefix `tenants`), `TenantListEndpoint`,
  `TenantGetEndpoint`, `TenantCreateEndpoint`, `TenantUpdateEndpoint`, **`TenantSuspendEndpoint`**,
  **`TenantReactivateEndpoint`** (D15), `TenantDeleteEndpoint`, `TenantMemberListEndpoint`
  (route `{tenantId}/members`), `TenantMemberAddEndpoint`, `TenantMemberUpdateRolesEndpoint`,
  `TenantMemberRemoveEndpoint`, **`TenantOnboardEndpoint`** (self-service, AC-133..AC-137) and
  **`TenantSwitchEndpoint`** (AC-025/AC-108/AC-139).

Both of the last two follow the documented `<Entity><Action>Endpoint` pattern in the
`coding-conventions` naming table — there is no `CreateOwnTenantEndpoint` and no
`SwitchTenantEndpoint` here. Lists reuse `ListRequestDto<TId>`, `ListRequestDtoValidator<TId>`,
`ListDto<T>` and `IQueryableExtension.Process`, with
`src/backend/Source/Features/Identity/Endpoints/Users/UserListEndpoint.cs` as the shape
(AC-061..AC-066).

### 3. Existing slices gaining predicates — governing skills: `backend-endpoint`, `notifications`, `file-storage`

- **Identity.** `Role.cs` gains `TenantId` + `ISoftDelete`; `RoleConfiguration.cs` gains the
  composite index (D11). `UserService.GetUserRolesAsync` / `GetUserPermissionsAsync` gain a tenant
  predicate (AC-027), and a new `TenantUsers()` helper backs AC-093/AC-094.
  `UserList/Get/Create/Update/Delete` and
  `RoleList/Get/Create/Update/Delete/ChangePermissions` change rows only — not routes, not payloads.
  `UserCreateEndpoint` additionally inserts a membership (AC-096). `GetInfoEndpoint` returns the
  caller's tenants and active tenant. `SignupEndpoint` stops assigning the retired `Public` role
  (AC-145, AC-118..AC-122). `AuthToken` gains `TenantId` and
  `Endpoints/Account/TokenService.cs` rebuilds the tenant from it. New
  `Core/TenantAuthorizationService.cs` (D3).
- **Notifications.** `Notification : ITenantScoped`; `NotificationService` gains a tenant-wide
  addressing method beside the existing user-targeted and platform-wide ones (AC-052..AC-054);
  `NotificationListEndpoint` and `NotificationGetUnreadCountEndpoint` union the platform-wide set
  through `AcrossAllTenants()` (D5); `NotificationMarkAllAsReadEndpoint`'s two raw statements get
  hand-written tenant predicates (D18).
- **FileManagement.** New `Core/Entities/StoredFile.cs` and
  `Core/Entities/Configuration/StoredFileConfiguration.cs` (`ITenantScoped`, `OwnerUserId`);
  `Core/FileService.cs` records and resolves attribution; `FileUploadEndpoint`, `FileGetEndpoint`
  and `FileDeleteEndpoint` require authentication (AC-098) and produce `crossTenantFileAccess`
  (AC-058). `IStorageProvider` and `LocalStorageProvider` are untouched.

### 4. Permissions — governing skill: `permissions`

`src/backend/Source/Permissions/Allow.cs` gains
`Tenant_View/Create/Update/Suspend/Reactivate/Delete`,
`TenantMember_View/Add/UpdateRoles/Remove` and `Platform_Administration`, mirrored one-for-one in
`src/frontend/web/allow.ts`. `PermissionDefinition.cs`, `PermissionDefinitionContext.cs` and
`PermissionDefinitionService.cs` gain the **in-memory** `IsPlatform` flag and
`GetPlatformPermissionNames()` (D16) — no `Permission` entity change, no `DataSeeder` change, no
migration. Each tenant's system-created administrator role is provisioned with exactly the
non-platform permissions (AC-042).

### 5. Errors — governing skill: `api-error-handling`

`src/backend/Source/ErrorHandling/ErrorCodes.cs` gains, covering AC-067 exhaustively:
`tenantNotFound`, `tenantSuspended`, `notTenantMember`, `noActiveTenant`,
`tenantIdentifierAlreadyExists`, `duplicateTenantMembership`, `lastTenantAdministrator`,
`systemCreatedTenantCannotBeModified`, `crossTenantFileAccess`, `tenantMembershipRevoked`,
`platformPermissionNotGrantable`, `concurrentModification`. Each gets an `error.server.<code>`
entry in all eight `src/frontend/web/public/locales/*.json` files (AC-069), alongside the twenty-six
that are there today. No new HTTP status is introduced.

### 6. Web — governing skills: `rtk-query-api`, `frontend-crud`, `frontend-page`, `ui-component`, `redux-state`

**New:** `store/api/tenancy/tenants/{tenants-api.ts,tenants-dtos.ts}` injected into the existing
`appApi`; `components/custom/tenant-switcher.tsx` (AC-073, reusing the existing dropdown from
`components/ui`); `app/[lang]/admin/(tenancy)/tenants/{list,create,update/[id],members/[id]}` with
their `_components` folders (AC-071/AC-072/AC-141);
`app/[lang]/(auth)/select-tenant/` (AC-140/AC-142); `app/[lang]/(auth)/no-tenant/`, which carries
AC-126 and AC-143 on one screen.

**Changed:** `App.tsx`, `store/slices/authSlice.ts`, `auth-urls.ts`, `nav-items.ts`,
`searchable-items.ts`, `allow.ts`, `components/layouts/header.tsx`, `components/custom/index.ts`,
`store/api/identity/{index.ts,account/account-api.ts,account/account-dtos.ts}`,
`store/api/identity/{users/users-dtos.ts,roles/roles-dtos.ts}`,
`store/api/file-management/files/{files-api.ts,files-dtos.ts}`,
`lib/utils/authentication-and-authorization.ts`.

### 7. Localization — governing skill: `localization`

Roughly forty new keys across the namespaces already in use — `page.tenants.*`, `navigation.*`,
`search.*`, `table.columns.*`, `form.label.*`, `validation.*`, `error.server.*` — and **no new
namespace**. All eight locale files (`ar, en, es, fr, hi, ru, ur, zh`) are one hotspot and get a
**single owning task**, authored English-first, so implementation waves cannot collide on them
(AC-075/AC-076). RTL (AC-077) is covered by the existing logical-property class conventions.

### 8. Tests — governing skills: `backend-tests`, `frontend-tests`

| File | Criteria |
| --- | --- |
| `src/backend/Tests/Architect/TenantScopingTests.cs` | AC-091. Walks `AppDbContext.Model`: every `ITenantScoped` type must carry a filter named `"Tenant"`, and every other entity type must appear in the reasoned exemption dictionary — `User`, `Permission`, `RolePermission`, **`UserRole` ("tenant derived through `Role.TenantId`", D13)**, `AuthToken`, `Token`, `NotificationVisit`, `Tenant` ("is the scope itself"). |
| `src/backend/Tests/Features/Tenancy/Endpoints/Tenants/*Tests.cs` | AC-086 — one file per endpoint, success path plus every failure branch (13 files). |
| `src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantPermissionTests.cs` | AC-088, using `TestRoles.LimitedTenantRole` below. |
| `src/backend/Tests/Features/Tenancy/Core/TenantIsolationTests.cs` | AC-087, AC-032, AC-111. |
| `src/backend/Tests/Features/Tenancy/Core/TenantUniquenessTests.cs` | AC-144, AC-147 — a soft-deleted tenant's identifier and a deleted tenant role's name are both refused. |
| `src/backend/Tests/Features/Tenancy/Core/BackgroundTenantScopeTests.cs` | AC-131 — its own non-HTTP fixture resolves `AppDbContext` outside a request, asserts a write fails while unresolved and succeeds under `BeginTenant`, and that neither "every tenant" nor "no rows" is the fallback. |
| `src/backend/Tests/Features/Tenancy/Endpoints/Tenants/TenantSuspensionTests.cs` | AC-089, AC-070 — the refusal is explanatory and the session survives it. |
| `src/backend/Tests/Features/Tenancy/Core/TenantConcurrencyTests.cs` | AC-081 (D14). |
| `src/backend/Tests/Features/Identity/Endpoints/Account/TenantSwitchTests.cs` | AC-148, AC-149, AC-108, AC-109, AC-123, AC-140. |
| `src/backend/Tests/Features/Identity/Endpoints/Account/TenantOnboardTests.cs` | AC-146, AC-135. |
| `src/backend/Tests/Features/FileManagement/Core/FileTenancyTests.cs` | AC-128 — including by supplying a stored file name, and an account-owned profile image readable in any tenant and in none. |
| `src/backend/Tests/Features/Notifications/Endpoints/Notifications/NotificationTenancyTests.cs` | AC-129. |
| `src/backend/Tests/Features/Notifications/Endpoints/Notifications/NotificationMarkAllAsReadTests.cs` | AC-130 — extended; the raw-SQL site of D18. |
| `src/backend/Tests/Seeder/TestTenants.cs` | **New**, mirroring `TestRoles`/`TestUsers` so parallel collections build their own tenants (AC-090). |
| `src/backend/Tests/Seeder/TestRoles.cs` | **`LimitedTenantRole`** added as a real artifact — a tenant role holding exactly one permission — because every seeded role holds every permission today, which is precisely why AC-088 exists. |
| `src/backend/Tests/AppTestsBase.cs`, `Tests/Seeder/TestsDataSeeder.cs`, `Tests/Seeder/TestUsers.cs` | Tenant-aware fixtures; `SetAuthTokenAsync` gains an optional tenant; the Testing seed produces `StoredFile` rows for seeded images. |
| `src/backend/Tests/Features/Identity/Endpoints/Roles/{RoleDeleteTests.cs,RoleCreateTests.cs}`, `Users/UserListTests.cs` | Updated for soft-deleted roles and tenant-scoped lists. |
| `src/frontend/web/store/slices/authSlice.test.ts` | AC-092, AC-132 — colocated and named after the file it covers (`authSlice.ts`), per `frontend-tests` and `coding-conventions`. |
| `src/frontend/web/lib/utils/authentication-and-authorization.test.ts` | AC-092 extended — a user whose permissions differ between two tenants. |

---

## Sequencing

**Wave 1 — prove the kernel before building on it.** Nothing else starts until this is green.

1. A spike test asserting, on Npgsql 10.0.0 against a real PostgreSQL, that: two named filters apply
   together; `IgnoreQueryFilters(["Tenant"])` suppresses *only* the tenant filter and leaves soft
   delete in force; and named filters behave as documented on `Include`-ed navigations and on
   `ExecuteUpdateAsync` / `ExecuteDeleteAsync`. If suppress-by-name does not work as documented,
   AC-034/AC-036 collapse and the isolation design is reworked here rather than in wave 5.
2. `ITenantScoped`, `ITenantContext`, `TenantFilter`, `AcrossAllTenants()`, `AppDbContextFactory`
   (D7), and `Tests/Architect/TenantScopingTests` (AC-091).
3. A confirmed `dotnet ef migrations add` scaffold against an empty database, proving D7 before
   anything depends on it.

**Wave 2 — schema and bootstrap.** One migration under `src/backend/Source/Migrations/`, written in
this order inside a single migration so no intermediate state is invalid:

1. create `tenancy.Tenants` and `tenancy.TenantMemberships`, and insert the bootstrap tenant using
   `TenancyConstants.BootstrapTenantId` — a compile-time constant **shared with `DataSeeder`**, so
   the seeder recognises the row the migration wrote instead of re-deriving it;
2. add `TenantId` as nullable to `Roles`, `Notifications` and the new `StoredFiles`;
3. backfill — existing roles other than `Admin` and `Public` to the bootstrap tenant, `Admin` to
   platform scope (`NULL`), every user-targeted notification to the bootstrap tenant, platform-wide
   notifications left `NULL`, one membership row per existing user; delete the `Public` role and its
   assignments (AC-145, AC-121);
4. tighten nullability where the column is required;
5. drop `IX_Roles_NameNormalized` and create `IX_Roles_TenantId_Name` with `NULLS NOT DISTINCT`
   (PostgreSQL 15+).

`src/backend/Source/Migrations/` is **not** copied into generated projects, so a new project's first
`dotnet ef migrations add Initial` produces the whole tenancy schema in one step (AC-085). The
backfill exists for this template repository's own development databases, so nobody ends up writing
a hand-rolled `UPDATE` against them. `src/backend/Source/Data/DataSeeder.cs` is then extended to
reconcile the bootstrap tenant, the platform `Admin` role and the admin membership idempotently
(AC-082..AC-084), and to delete the `Public` role idempotently rather than leaving an orphan role
with no declared scope.

**Wave 3 — claims and enforcement.** `ClaimConstants`, `Helper.CreateClaims`, `AuthToken.TenantId`,
`Endpoints/Account/TokenService.SetRenewalPrivilegesAsync`, the merged session-plus-tenant
middleware (D8/D9), `TenantContextProcessor` and `[AllowNoTenant]`, and the `Program.cs` wiring.
This must precede every endpoint task, because the tenant claim doubles as the authorization
boundary and the three claim-issuance points — `TokenEndpoint`,
`TokenService.SetRenewalPrivilegesAsync` and `TenantSwitchEndpoint` — must stay in step or a
refresh silently grants the previous tenant's permissions (AC-108/AC-109).

**Wave 4 — the Tenancy slice** (feature module, permission provider, two services, thirteen
endpoints) **in parallel with** the predicate edits to Identity, Notifications and FileManagement;
these are file-disjoint. `Allow.cs`, `allow.ts`, the permission provider and `ErrorCodes.cs` land at
the head of this wave as single-owner tasks, because many later tasks read them.

**Wave 5 — web**, in parallel with the backend test files. Within the web work, `get-info` +
`authSlice` + `App.tsx` land before the switcher and the screens. **All eight locale files are one
task.**

**Wave 6 — verification.** `/verify` against all 149 criteria.

---

## Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| **EF Core 10 named query filters are the load-bearing assumption.** If `IgnoreQueryFilters(["Tenant"])` does not suppress exactly one filter on Npgsql 10.0.0, AC-034/AC-036 collapse and the isolation design needs rework. | Wave 1 exists only to prove this, against a real database, before anything is built on it. |
| **`AppDbContextFactory` passing `null!`** would make `dotnet ef migrations add` — the first command a generated project runs — dereference the tenant accessor. | Resolved by decision D7, not deferred to a risk: null-conditional read plus real design-time no-op services, with a wave-1 scaffold check as the gate. |
| **`NULLS NOT DISTINCT` is PostgreSQL 15+.** Without it, platform role names are not unique at all, because every `NULL` tenant compares distinct. | Explicit in D11 and in `RoleConfiguration`; the minimum PostgreSQL version is recorded in the migration comment, and the wave-2 task asserts that a duplicate platform role name is refused. |
| **Making `Role` `ISoftDelete` changes deletion semantics for an entity that already has tests.** | `RoleDeleteTests`, `RoleCreateTests` and `DataSeeder`'s permission-stripping loop are named tasks in wave 4; the seeder must not resurrect soft-deleted roles. |
| **Requiring authentication on file upload and download breaks existing image URLs.** `ImagePreview`, `components/ui/form/file-upload.tsx` and the Testing-environment seed data all depend on the current anonymous behaviour. | Named explicitly in the FileManagement task set: the Testing seeder must produce `StoredFile` rows for seeded images, and both web components are checked in the same task. |
| **Tenant-scoping user administration silently changes what existing `UserList`/`UserGet` tests see.** | `TestsDataSeeder` and `TestUsers` are updated in the same wave as `UserListTests`, and `TestRoles.LimitedTenantRole` is added *without* changing what existing tests assume about the existing seeded roles. |
| **Eight locale files are the largest merge-conflict surface in the change.** | One owning task; `spec-implement` treats `public/locales/*.json` as a hotspot. |
| **Blast radius.** This touches roughly 95 files, about 40 of which are one-line predicate, claim or key additions. That is inherent to the spec's "routes unchanged, rows changed" framing (AC-093..AC-096, AC-110..AC-113) — but it is 95 files, not the handful the framing implies. | Stated plainly so nobody plans around a smaller number; the wave order is what keeps waves 4 and 5 parallelisable despite it. |
| **Scale.** 149 criteria across both stacks, two new entities, a schema-wide filter change, thirteen new endpoints and five new screens. A realistic task count is **95-105**, not the ~68 a first pass suggests, and a single `/implement` run is unlikely to converge on the first `/verify`. | Waves 1-3 are deliberately serial and small, so the reviewable risk is front-loaded and waves 4-5 can be re-run per task. |
| **Slice tension, stated plainly.** `Features/Tenancy` and `Features/Identity` are two slices serving one authorization model, connected by one `[AllowOutside]` interface (D3). That is a real seam: a change to how a member's roles work touches both. It is the lesser cost — the alternative makes `Features/Identity` the repository's centre of gravity indefinitely and gives tenancy none of the slice furniture every other capability here has. | D3 keeps the seam to one interface, one direction, and `Guid`-only signatures, so it stays small and greppable. |
| **The tenant claim is the authorization boundary,** so a refresh that re-derives roles without re-deriving the tenant would silently grant the previous tenant's permissions. | `AuthToken.TenantId` (clean-slice graft) plus wave 3's single-claim-issuance rule, plus `TenantSwitchTests` covering AC-108/AC-109 directly. |

---

## Explicitly not doing

- **No `AuthorizationStamp`, and no per-write "remember to bump" contract anywhere.** Freshness is
  recomputed per request (D8).
- **No `IsPlatform` column on the `Permission` entity**, no seeder branch for it, no migration for
  it (D16).
- **No `TenantId` on `UserRole`** — its tenant is `Role.TenantId`, recorded as a reasoned exemption
  in the architecture check (D13).
- **No second marker interface, and no per-entity filter expressions inside `AppDbContext`**
  (D4, D5).
- **No `TenantSetStatusEndpoint`** — suspend and reactivate are separately gated endpoints (D15).
- **No `localStorage`, `sessionStorage` or cookie of our own for the active tenant** (D19).
- **No raw `IgnoreQueryFilters(["Tenant"])` call sites** — `AcrossAllTenants()` is the only opt-out.
- **No new HTTP status codes**, no new problem-response shape, no new web API client, no new Redux
  slice, no new translation namespace.
- **No changes to `IStorageProvider`, `LocalStorageProvider.GetSafePath`, or the `ImagePreview`
  contract** (D17).
- **No rewrite of `NotificationMarkAllAsReadEndpoint`'s `ON CONFLICT` statement** (D18).
- **No project-specific hardcoding** — everything here ships to every generated project, so no
  tenant name, domain or deployment topology appears in code (spec "Genericity").
- Everything under the spec's own **Out of scope** heading: database-per-tenant or schema-per-tenant
  isolation, billing and quotas, email invitations, tenant-branded public pages, per-tenant
  branding, subdomain addressing, migrating an existing single-tenant deployment, cross-tenant
  reporting and export, hard deletion and retention windows, impersonation, and a per-tenant audit
  log beyond the audit columns entities already carry.
