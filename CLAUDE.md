# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repository is

Two coupled deliverables live here:

- `src/` — the **project template** itself: an ASP.NET 10 (FastEndpoints) backend and a Next.js 16 frontend. It is a working application (Identity, FileManagement, Notifications), but its primary purpose is to be the source that gets copied into new projects.
- `tool/EasyForNetTool` — the **`dotnet efn` CLI** (packaged as `EasyForNetTool`, command `dotnet-efn`). `efn cp -n <name>` clones `https://github.com/ma496/EasyForNet.git` into `%LOCALAPPDATA%/EasyForNet/Templates/<version>`, checks out the tag `v{tool version}`, copies `src/`, and rewrites namespaces/project names (see `Generator/CreateProjectGenerator.cs`, `NamespaceRewriter.cs`).

Consequences to keep in mind when editing:

- Changes under `src/` ship to every newly scaffolded project, so keep the template generic (no project-specific hardcoding).
- The tool resolves the template by **git tag matching its own version**. `publish-package.sh` enforces this: clean working tree → run tool tests → `dotnet pack` → create/push tag `v$VERSION` → `dotnet nuget push`.
- `CreateProjectGenerator` copies an explicit list of root files/directories (`.editorconfig`, `.gitignore`, `global.json`, `.config`, `.vscode`, `.claude` minus the `new-project` and `template-maintenance` skills, and a `CLAUDE.md` written from the embedded `new-project-claude.md`). If a new root-level file should reach generated projects, it must be added to that list. Inside `.claude` the rule inverts: exclusion is by directory *name* at any depth, so `.claude/skills`, `.claude/workflows` and `.claude/commands` all ship with no generator change. Markdown under `.claude` is rewritten on copy (`Backend.` → the new root namespace, `EasyForNet.slnx` → `<Name>.slnx`), so keep namespace references there in that qualified form — and out of `.js` files entirely, which are copied byte-for-byte.
- `specs/` is deliberately not copied: each project accumulates its own specifications.
- Migrations are deliberately **not** copied into generated projects (`CopyDirectory(..., ["Migrations"])`); new projects run `dotnet ef migrations add Initial` themselves.

## Commands

Backend (`src/backend`):

```sh
dotnet tool restore                 # restores dotnet-ef 10 pinned in .config/dotnet-tools.json
dotnet build EasyForNet.slnx        # or dotnet build from src/backend/Source
dotnet ef migrations add <Name> --project src/backend/Source/Backend.csproj
dotnet ef database update --project src/backend/Source/Backend.csproj
dotnet run --project src/backend/Source/Backend.csproj
```

Backend tests (`src/backend/Tests`) — **require a running PostgreSQL** matching `appsettings.Testing.json`; the Testing environment migrates and seeds on startup:

```sh
dotnet test src/backend/Tests/Backend.Tests.csproj
dotnet test src/backend/Tests/Backend.Tests.csproj --filter "FullyQualifiedName~UserCreateTests"
dotnet test src/backend/Tests/Backend.Tests.csproj --filter "FullyQualifiedName~UserCreateTests.Valid_Input"
```

Frontend (`src/frontend/web`, Node >= 24):

```sh
npm install
npm run dev
npm run build
npm run lint                        # eslint flat config
npm run test                        # vitest run
npx vitest run lib/utils/redirect.test.ts
```

Tool:

```sh
dotnet test tool/EasyForNetTool.Tests/EasyForNetTool.Tests.csproj
./publish-package.sh                # interactive: version prompt + NuGet publish confirmation
```

## Backend architecture

**Vertical slices under `Features/`.** Each feature is `Features/<Feature>/{Core,Endpoints}`:

- `Core` holds entities (`Core/Entities`, EF configuration in `Core/Entities/Configuration`), services, settings, and the feature's `IPermissionDefinitionProvider`.
- `Endpoints/<Area>` holds one file per endpoint plus an `<Area>Group.cs` that owns the route prefix.
- `<Feature>Feature.cs` implements `IFeature.AddServices`. `Helper.AddFeatures` reflects over the assembly at startup and calls it — features are never registered by hand in `Program.cs`.

**One endpoint = one file.** `UserCreateEndpoint.cs` is the canonical shape: the `sealed class ...Endpoint : Endpoint<TReq, TRes>`, its `...Request`, a FluentValidation `...Validator : Validator<TReq>`, the `...Response`/DTOs, and the Riok.Mapperly `[Mapper] partial class ...Mapper` all live together. Types are internal where possible; `Meta.cs` grants `InternalsVisibleTo("Backend.Tests")` so tests can reference them.

**Global usings live in `Meta.cs`** (FastEndpoints, FluentValidation, Mapperly, EF Core, `Backend.ShareData`, `Backend.Base.Dto`, `Backend.Permissions`, …). Do not re-add those per file; add new project-wide ones there.

**Feature isolation is enforced by tests.** `Tests/Architect/FeatureDependencyTests` fails if a type under `Backend.Features.X` depends on a type under `Backend.Features.Y` unless that type is marked `[AllowOutside]`. `[NoDirectUse]` (with `[BypassNoDirectUse]` as the escape hatch) enforces consuming a class through its interface. `Tests/Architect/Features/Feature{A,B}` are fixtures that exercise the rules themselves — not real features.

**Permissions** are string constants in `Permissions/Allow.cs`, declared as a hierarchy by each feature's `IPermissionDefinitionProvider`, enforced on endpoints via `Permissions(Allow.X)`, and reconciled into the database by `ShareData/DataSeeder` on every startup (adds/renames/deletes rows and strips deleted permissions from roles). Adding a permission means: constant in `Allow.cs` → definition in the provider → mirror the constant in `src/frontend/web/allow.ts`. A definition may also call `.RequireFeatures(...)` — see **Features (entitlements)** below.

Each definition also carries a `PermissionScope` — `Tenant` (the default), `Platform` or `Both` — and `SessionGrants` narrows the permission claims a session is minted with to the scope it acts in: `Platform` + `Both` acting in no tenant, `Tenant` + `Both` acting inside one, nothing at all for an ordinary account with no tenant. Which roles count follows the scope too: inside a tenant only that tenant's own roles, in no tenant only the platform roles (`Role.TenantId == null`) — so a platform role holds only `Platform` + `Both` permissions (`ChangePermissionsEndpoint` refuses a `Tenant` one with `tenantPermissionNotGrantable`, as it refuses a `Platform` one on a tenant role). **Permissions are the only authorization input**, so a tenant-scoped operation is kept out of platform scope by declaring a `Tenant`-scoped permission and by nothing else — there is no endpoint attribute beside it. The separate `User.IsPlatform` column names the account's tier, travels as the `is_platform` claim, and is read only where the tier itself is the question — sign-in, the Hangfire dashboard, leaving a tenant for platform scope, what tier a newly created account gets, and who a tenant administers. It is no standing inside a tenant: a platform account enters only a tenant it is a member of, and acts there on the roles that membership holds. Nor is it administered from inside one: `IUserService.TenantUsers()`, the tenant member endpoints and the role user counts leave platform accounts out unless the caller is a platform account acting in no tenant, so a tenant's administrators can neither see, add, re-role, remove, edit nor delete one.

**Features (entitlements)** answer a different question from permissions: not *may this caller do it*
but *does this tenant's plan include it at all* — the same answer for everyone in the tenant, its
administrator included. Entitlements are a fact about a tenant, so the whole system lives inside the
tenancy slice, in `Features/Tenancy/Core/FeatureManagement/`. The vocabulary other slices declare
their features in - and the services they ask questions of - is published with `[AllowOutside]`, the
same way `ITenantContext` is; everything that resolves or stores a value stays private to the slice.
Each slice declares its own features in `Core/<X>FeaturesProvider.cs`, beside the
`<X>PermissionsProvider` it already has; `<X>Feature.cs : IFeature` remains the slice's DI module and
has nothing to do with this. Adding a feature means: constant in
`Features/Tenancy/Core/FeatureManagement/FeatureNames.cs` → definition in the slice's provider →
mirror the constant in `src/frontend/web/feature-names.ts`. **Every feature ships
enabled by default**, so an installation that has sold nothing behaves as though the system were not
there.

A feature's value is resolved for one **target** through a chain, first answer winning: the tenant's
own override → what its `Edition` (plan) grants → the deployment's `FeatureManagement` configuration
section → the value the definition declares. `Edition` and `Tenant.EditionId` live in the tenancy
slice; the stored values are `FeatureValue` rows keyed by `(ProviderName, ProviderKey)` rather than by
a tenant column, which is what lets them be read while a session is minted and no tenant scope exists.
A child feature is not in force whenever an ancestor toggle is off. `IFeatureValueResolver` takes the
target as an argument and never reads ambient state; `IFeatureChecker` is the thin convenience over it
for endpoint code, which fills the target in from `ITenantContext` and refuses loudly when no scope has
been established.

**A permission may declare `.RequireFeatures(...)`**, on a leaf or on a group node, in which case every
permission beneath it inherits the requirement. `IPermissionFeatureFilter` is the only place that rule
is written, and its three callers must not be allowed to disagree: `SessionGrants` (the claims a
session is minted with), `GetDefinePermissionsEndpoint` (the catalogue a role is edited from) and
`GetInfoEndpoint` (what the web app gates on). Enforcement is **mint-time only** — a feature change
bites at the caller's next token renewal, exactly as a role change does, which is what keeps "decided
once, when a token is minted" true. Platform scope narrows nothing: an account acting in no tenant is
inside no plan, and gating the permissions that administer the feature system would make a feature
switched off impossible to switch back on. Two architecture tests hold the line — no `Platform`-scoped
permission may require a feature, and every feature a permission names must actually be declared.

Two consequences worth keeping in mind. Gating never revokes anything: `DataSeeder` still persists a
row for every permission and still grants the administrator roles the whole scope, so turning a
feature back on restores the permission with nothing to re-grant — and `ChangePermissionsEndpoint`
carries plan-hidden grants through a replacement rather than reading the form's silence about them as
a removal. And an endpoint gated on a feature alone, with no permission to hang it on, calls
`featureChecker.CheckEnabledAsync(...)` in its handler rather than declaring an attribute, so
"permissions are the only authorization input" stays true: entitlement is a business precondition, and
it answers 403 `featureDisabled` through `ExceptionProcessor`.

**Data access.** `AppDbContext` applies entity configurations from the assembly, installs a global soft-delete query filter for `ISoftDelete`, and fills audit/normalized properties on save. List endpoints take a `ListRequestDto<TId>` and call `IQueryableExtension.Process(request)` for sorting/paging; sortable fields must be whitelisted in the request validator.

**Auth.** A `Jwt_Or_Cookie` policy scheme picks JWT bearer when an `Authorization: Bearer` header is present, cookies otherwise. Roles, permissions and the tenant are decided once, when a token is minted (sign-in, refresh, tenant switch/exit), and trusted until that token is replaced — no request re-reads them, and `TenantContextProcessor` establishes the tenant from the `tenant_id` claim alone. A change to what a caller may do therefore takes effect at their next token renewal, which `Auth:AccessTokenValidity` bounds for cookie and bearer clients alike — the auth cookie is configured with `SlidingExpiration = false` precisely so a browser session expires on that clock and is forced through the refresh rather than being re-issued with the ticket it already had. The refresh path is the one place a live session is re-examined: `TokenService.SetRenewalPrivilegesAsync` refuses a deactivated account and drops a tenant that has been suspended, deleted or left, renewing the session without one rather than ending it. Sign-in puts an ordinary account into exactly one tenant — its single active membership, or the `TenantIdentifier` it supplies — and refuses with `tenantRequired` otherwise; a platform account signs in with no tenant unless it names one. Every tenant a session enters — at sign-in, on `POST /tenants/switch` and at refresh — needs a live membership of it, whatever the account's tier; a platform account returns to platform scope through `POST /tenants/exit`. Refresh tokens and forgot-password tokens are cleaned by Hangfire recurring jobs registered at the end of `Program.cs`.

**Errors.** Endpoints call `ThrowError(message, ErrorCodes.X)` with codes from `ErrorHandling/ErrorCodes.cs`; FastEndpoints serializes ProblemDetails with the error code included, and `ExceptionProcessor` / `ToLargePayloadProcessor` / `UnsupportedMediaTypeResponseProcessor` are wired globally in `Program.cs`.

**Startup guards** worth knowing before changing configuration: the JWT key placeholder is rejected outside Development/Testing, `Database:ApplyMigrationsOnStartup` defaults to true only in Development/Testing, and the `Payload`/`Web`/`Auth` options are `ValidateOnStart`. Under `Testing` the host additionally runs no Hangfire worker, logs at `Warning` and applies no request rate limit — defaults that live in `Program.cs` because `appsettings.Testing.json` is not in source control.

## Backend tests

xUnit v3 + `FastEndpoints.Testing`. `App : AppFixture<Program>` runs the host with environment `Testing` and is registered as an **assembly fixture** in `Tests/Meta.cs`, so it is built, migrated and seeded once for the whole run. `AppTestsBase` gives each test its own `Client` and its own DI scope — `DbContext`, `TenantContext` and `Service<T>()` all resolve from it — plus `SetAuthTokenAsync()` (defaults to the default tenant's administrator `tenantadmin` / `Admin#123`; `SetPlatformAdminAuthTokenAsync()` signs in as the platform administrator `admin`) and `CreateAdminUserAsync`. Tests call endpoints type-safely — `Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, UserCreateResponse>(request)` — assert with FluentAssertions, and build payloads with Bogus `Faker<T>`. Shared fixtures live in `Tests/Seeder` (`TestRoles`, `TestUsers`, `TestsDataSeeder`); reuse them instead of creating ad-hoc roles. **Test classes run in parallel**, one collection per class, so a test must not depend on global database state it did not create; a class that shares a resource with another names a `[Collection]` for itself (`Notifications`, `FileManagement`, `BootstrapTenant`, `FeatureManagement`). The test host substitutes a cheap password hasher and a mail service that sends nothing (`Tests/Fakes`); under `Testing`, `Program.cs` also runs no Hangfire worker, logs at `Warning` and lifts the request rate limit.

## Frontend architecture

Next.js App Router under `app/[lang]/` — **every route is locale-prefixed**. Route groups: `(public)`, `(auth)` (signin/signup/profile/password flows), and `admin/`. Page components are server components that resolve `params`, fetch a title via `getServerTranslation(lang, key)`, and render an interactive client component from a sibling `_components/` folder inside `AdminPageContent`.

`proxy.ts` (Next 16's renamed middleware) does two things: locale negotiation (rewrite for the default locale, redirect otherwise; locales in `i18n/config.ts`, messages in `public/locales/<locale>.json`) and auth gating based on `auth-urls.ts` plus the auth cookie.

**Data layer is RTK Query.** `store/api/_app-api.ts` defines the single `appApi` with a `baseQueryWithReauth` that serializes refresh attempts through an `async-mutex` and signs the user out (redirecting to `/signin?redirect=…`) when refresh fails. Feature APIs live in `store/api/<feature>/<area>/{<area>-api.ts,<area>-dtos.ts}` and attach via `appApi.enhanceEndpoints({ addTagTypes: [...] }).injectEndpoints(...)` — never call `createApi` again. DTO types mirror the backend request/response classes by name.

Client permission checks use the `Allow` map in `allow.ts`, kept in sync with the backend constants. Shared pieces: `components/{custom,ui,layouts,notifications}`, `hooks/` (`use-table-url-state`, `use-localized-router`, `use-debounce`, `use-notification-hub` — polling, not a socket), `lib/utils/` (API error helpers, auth helpers). Navigation and global search entries are declared in `nav-items.ts` and `searchable-items.ts`.

Adding a language means adding it to `i18n/config.ts` and adding `public/locales/<code>.json`; the CLI's `-m false` (default) mode ships English only, so check how the generator filters locale files when changing this.

## Spec-driven development

Features large enough to be worth specifying go through four chained dynamic workflows in
`.claude/workflows/`, driven by four slash commands. Each stage writes documents into
`specs/<NNN-slug>/` and stops so a human can read them:

```
/specify   <request>            spec-specify    -> spec.md with numbered EARS acceptance criteria
/plan      [NNN-slug]           spec-plan       -> plan.md, four contract documents, tasks.md
/implement [NNN-slug]           spec-implement  -> code, in file-disjoint waves
/verify    [NNN-slug]           spec-verify     -> verification.md, plus the remaining work as tasks
```

The stages are separate runs because a workflow cannot ask a question while it runs — every point
where a human decision belongs is a stage boundary. `AC-nnn` and `T-nnn` ids are permanent and
tie criteria to tasks to evidence. See the `spec-driven` skill for the loop, the document
templates and the completeness checklist; `specs/README.md` describes the directory layout.

One full loop is roughly 100 subagent calls on a small feature and around 230 on a large one, so it
is for real features, not one-line fixes.

The scripts are `.js`, and **the generator does not rewrite `.js`** — it rewrites only markdown
under `.claude`. Keep namespaces, the solution file name and project file names out of the workflow
scripts; put anything repo-specific in `.claude/skills/spec-driven/SKILL.md` instead.

## Task guides

`.claude/skills/` holds step-by-step guides for the recurring tasks here. Consult the matching one before writing code.

- Process: `spec-driven`
- Cross-cutting: `coding-conventions`
- API: `backend-feature`, `backend-endpoint`, `backend-entity`, `backend-tests`, `permissions`, `feature-management`, `background-jobs`, `file-storage`, `notifications`
- Web: `rtk-query-api`, `frontend-page`, `frontend-crud`, `ui-component`, `redux-state`, `localization`, `frontend-tests`
- Spanning both: `api-error-handling`
- This repository and the CLI: `new-project` (scaffolding), `template-maintenance`

Every skill except those last two ships to generated projects, so keep them generic.
