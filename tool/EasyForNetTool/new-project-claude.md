# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project layout

- `src/backend/Source` — ASP.NET 10 API built on FastEndpoints, EF Core (PostgreSQL) and Hangfire.
- `src/backend/Tests` — xUnit v3 integration tests that boot the real API host.
- `src/frontend/web` — Next.js 16 App Router frontend (React 19, Redux Toolkit Query, Tailwind).

## Commands

Backend:

```sh
dotnet tool restore                 # restores dotnet-ef pinned in .config/dotnet-tools.json
dotnet build EasyForNet.slnx
dotnet ef migrations add <Name> --project src/backend/Source
dotnet ef database update --project src/backend/Source
dotnet run --project src/backend/Source   # needs PostgreSQL and Redis (ConnectionStrings:Redis)
```

Backend tests — **require a running PostgreSQL** matching `appsettings.Testing.json` (not Redis: the Testing host keeps sessions in memory); the Testing environment migrates and seeds the database on startup:

```sh
dotnet test src/backend/Tests
dotnet test src/backend/Tests --filter "FullyQualifiedName~UserCreateTests"
dotnet test src/backend/Tests --filter "FullyQualifiedName~UserCreateTests.Valid_Input"
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

Everything at once, from the repository root (the gate needs PostgreSQL running; the live check also needs Redis):

```sh
npm run gate                        # build, backend tests, web lint/tsc/vitest, engine + hook tests, next build
npm run gate -- --fast              # the same without the production web build
npm run verify -- --autostart       # the gate, plus the live API smoke check when src/backend/Source changed
```

The API that `verify` starts for the live check waits on `scripts/pg-ready.mjs` and `scripts/redis-ready.mjs` (a TCP probe of `ConnectionStrings:Redis`), both listed under `verify.service.dependsOn` in `agentic.config.json`.

Default credentials seeded on first run: `admin` / `Admin#123` (the platform account — `IsPlatform`, no membership, so it works in platform scope only until it is added to a tenant) and `tenantadmin` / `Admin#123` (default tenant administrator).

## Backend architecture

**Vertical slices under `Features/`.** Each feature is `Features/<Feature>/{Core,Endpoints}`:

- `Core` holds entities (`Core/Entities`, EF configuration in `Core/Entities/Configuration`), services, settings, and the feature's `IPermissionDefinitionProvider`.
- `Endpoints/<Area>` holds one file per endpoint plus an `<Area>Group.cs` that owns the route prefix.
- `<Feature>Feature.cs` implements `IFeature.AddServices`. `Helper.AddFeatures` reflects over the assembly at startup and calls it — features are never registered by hand in `Program.cs`.

**One endpoint = one file.** `Features/Identity/Endpoints/Users/UserCreateEndpoint.cs` is the canonical shape: the `sealed class ...Endpoint : Endpoint<TReq, TRes>`, its `...Request`, a FluentValidation `...Validator : Validator<TReq>`, the `...Response`/DTOs, and the Riok.Mapperly `[Mapper] partial class ...Mapper` all live together. Types are internal where possible; `Meta.cs` grants `InternalsVisibleTo` to the test project so tests can reference them.

**Global usings live in `Meta.cs`** (FastEndpoints, FluentValidation, Mapperly, EF Core, the `ShareData`, `Base.Dto` and `Permissions` namespaces, …). Do not re-add those per file; add new project-wide ones there.

**Feature isolation is enforced by tests.** `Tests/Architect/FeatureDependencyTests` fails if a type under `<RootNamespace>.Features.X` depends on a type under `<RootNamespace>.Features.Y` unless that type is marked `[AllowOutside]`. `[NoDirectUse]` (with `[BypassNoDirectUse]` as the escape hatch) enforces consuming a class through its interface. `Tests/Architect/Features/Feature{A,B}` are fixtures that exercise the rules themselves — not real features.

**Permissions** are string constants in `Permissions/Allow.cs`, declared as a hierarchy by each feature's `IPermissionDefinitionProvider`, enforced on endpoints via `Permissions(Allow.X)`, and reconciled into the database by `ShareData/DataSeeder` on every startup (adds/renames/deletes rows and strips deleted permissions from roles). Adding a permission means: constant in `Allow.cs` → definition in the provider → mirror the constant in `src/frontend/web/allow.ts`. A definition may also call `.RequireFeatures(...)` — see **Features (entitlements)** below.

Each definition also carries a `PermissionScope` — `Tenant` (the default), `Platform` or `Both` — and `SessionGrants` narrows the permissions a session is created with to the scope it acts in: `Platform` + `Both` acting in no tenant, `Tenant` + `Both` acting inside one, nothing at all for an ordinary account with no tenant. Which roles count follows the scope too: inside a tenant only that tenant's own roles, in no tenant only the platform roles (`Role.TenantId == null`) — so a platform role holds only `Platform` + `Both` permissions (`ChangePermissionsEndpoint` refuses a `Tenant` one with `tenantPermissionNotGrantable`, as it refuses a `Platform` one on a tenant role). **Permissions are the only authorization input**, so a tenant-scoped operation is kept out of platform scope by declaring a `Tenant`-scoped permission and by nothing else — there is no endpoint attribute beside it. The separate `User.IsPlatform` column names the account's tier, travels as the `is_platform` claim, and is read only where the tier itself is the question — sign-in, the Hangfire dashboard, leaving a tenant for platform scope, what tier a newly created account gets, and who a tenant administers. It is no standing inside a tenant: a platform account enters only a tenant it is a member of, and acts there on the roles that membership holds. Nor is it administered from inside one: `IUserService.TenantUsers()`, the tenant member endpoints and the role user counts leave platform accounts out unless the caller is a platform account acting in no tenant, so a tenant's administrators can neither see, add, re-role, remove, edit nor delete one.

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
mirror the constant in `src/frontend/web/feature-names.ts`. **Every feature ships enabled by
default**, so an installation that has sold nothing behaves as though the system were not there.

A feature's value is resolved for one **target** through a chain, first answer winning: the tenant's
own override → what its `Edition` (plan) grants → the deployment's `FeatureManagement` configuration
section → the value the definition declares. `Edition` and `Tenant.EditionId` live in the tenancy
slice; the stored values are `FeatureValue` rows keyed by `(ProviderName, ProviderKey)` rather than by
a tenant column, which is what lets them be read while a session is created and no tenant scope exists.
A child feature is not in force whenever an ancestor toggle is off. `IFeatureValueResolver` takes the
target as an argument and never reads ambient state; `IFeatureChecker` is the thin convenience over it
for endpoint code, which fills the target in from `ITenantContext` and refuses loudly when no scope has
been established.

**A permission may declare `.RequireFeatures(...)`**, on a leaf or on a group node, in which case every
permission beneath it inherits the requirement. `IPermissionFeatureFilter` is the only place that rule
is written, and its two callers must not be allowed to disagree: `SessionGrants` (the permissions a
session is created with) and `GetDefinePermissionsEndpoint` (the catalogue a role is edited from).
`GetInfoEndpoint` (what the web app gates on) agrees with the first by construction, because it reports
the session's own roles and permissions rather than recomputing them. Plan gating is
**computed when the session is created, and the session is revoked when the plan changes**: changing a
tenant's edition or its feature values ends every session in that tenant, and changing an edition's
feature values or deleting it ends every session in every tenant on it (see **Auth**), so the tenant's
users come back under the new plan when they next sign in. A change to the `FeatureManagement`
configuration section revokes nothing and reaches a session only when it is next replaced;
`IFeatureChecker` calls read the current value on every call. Platform scope narrows nothing: an account acting in no tenant is
inside no plan, and gating the permissions that administer the feature system would make a feature
switched off impossible to switch back on. Two architecture tests hold the line — no `Platform`-scoped
permission may require a feature, and every feature a permission names must actually be declared.

Two consequences worth keeping in mind. Gating never removes a grant: `DataSeeder` still persists a
row for every permission and still grants the platform and bootstrap-tenant administrator roles their
whole scope (other tenants' administrator roles get theirs from
`ProvisionTenantAdministratorRoleAsync` when the tenant is created or gains its first member), so
turning a feature back on restores the permission with nothing to re-grant — and `ChangePermissionsEndpoint`
carries plan-hidden grants through a replacement rather than reading the form's silence about them as
a removal. And an endpoint gated on a feature alone, with no permission to hang it on, calls
`featureChecker.CheckEnabledAsync(...)` in its handler rather than declaring an attribute, so
"permissions are the only authorization input" stays true: entitlement is a business precondition, and
it answers 403 `featureDisabled` through `ExceptionProcessor`.

A numeric feature is a **limit**, and exceeding one throws `FeatureLimitExceededException`, which
`ExceptionProcessor` answers with 403 `featureLimitExceeded`. Two are enforced. `Identity.MaxUserCount`
is checked in `TenantMembershipService` under the tenant row lock, so concurrent additions cannot both
take the last seat: `AddAsync` checks it for every membership, and `UserCreateEndpoint` calls
`ReserveSeatAsync` inside the same transaction as the account it creates. `GetSeatsAsync` is the one
reading of seats taken and seats allowed; platform accounts take none. The guard enforces it, and
`GET /users/seats` (the acting tenant) and `GET /tenants/{tenantId}/members/seats` (the route tenant)
report it, so the web app disables its create and add-member buttons exactly when the API would refuse. `FileUploadEndpoint` holds every upload
made inside a tenant, account-owned ones included, to `FileManagement.Enabled` and
`FileManagement.MaxFileSizeMb`; an upload in platform scope answers to `Payload:MaximumSize` alone.
On the web, `FileUpload` and `MultiFileUpload` apply the stricter of their `maxSizeBytes` and
`usePlanMaxUploadBytes()`.

**Localization.** Translations are served by the API, not bundled with the web app. The shipped strings are nested JSON files in `Features/Localization/Core/Resources/<code>.json`, embedded in the assembly and flattened to dotted keys (`common.save`) once by `LocalizationResourceStore`; `LanguageCatalog` holds each code's display name and `isRtl`. On top of them sit two tenant-optional (`IMayHaveTenant`, `TenantId == null` meaning the platform) tables: `LocalizationText` overrides one key in one culture, and `LanguageSetting` holds a scope's enabled cultures and default. A text resolves first answer wins: the acting tenant's override → the platform's → the shipped value for the culture → the shipped English value. Languages resolve per row, not per key: the tenant's own `LanguageSetting` row → the platform's → every shipped culture enabled with no default. The culture served is the one requested if enabled, else the scope's default, else `en`, else the first enabled. The `/localization` endpoints go through `ILocalizationService`, feature-internal like every other slice service: `GET resources/{culture}` is anonymous (an anonymous caller or a session in no tenant sees platform overrides only), and `GET/PUT/DELETE texts` and `GET/PUT/DELETE languages` edit the acting scope's own overrides under `Localization.View` / `Localization.Update`, both `PermissionScope.Both` and gated on no feature. `Tests/Features/Localization/Core/LocalizationResourceStoreTests` keeps every shipped file on English's exact key set, with no empty value, no value equal to its key, and a `LanguageCatalog` entry per file. The same resolution chain, through the narrow `[AllowOutside]` `IErrorMessageLocalizer` (`ErrorMessageLocalizer` is a thin wrapper over `ILocalizationService`, so the global error plumbing outside the feature never depends on the full service), is what puts a coded API error's message into the request's own culture — see Errors below.

**Data access.** `AppDbContext` applies entity configurations from the assembly, installs a global soft-delete query filter for `ISoftDelete`, and fills audit/normalized properties on save. List endpoints take a `ListRequestDto<TId>` and call `IQueryableExtension.Process(request)` for sorting/paging; sortable fields must be whitelisted in the request validator.

**Auth.** A `Jwt_Or_Cookie` policy scheme picks JWT bearer when an `Authorization: Bearer` header is present, cookies otherwise. **A token or auth cookie carries only who the caller is (`ClaimTypes.NameIdentifier`) and which session (`sid`)**; everything else — username, email, tier, acting tenant, roles, permissions — is a `SessionRecord` in `ISessionStore` (`Features/Identity/Core/Sessions`). The store is Redis (`ConnectionStrings:Redis`, every key prefixed with `Redis:InstanceName`) in every environment but `Testing`, where one in-memory store is shared by the whole run so the tests need only PostgreSQL. Sign-in, refresh and tenant switch/exit go through `ISessionIssuer`, which computes the session with `SessionGrants` and writes it to the store for `Auth:RefreshTokenValidity`; refresh and switch/exit delete the session they replace.

**Every authenticated request reads its session.** `SessionAuthentication` chains `SessionPrincipalValidator` onto both handlers' events, so it runs after the token or cookie authenticates and before authorization. It loads the session `sid` names: one that is missing, expired or belongs to another account than `NameIdentifier` fails authentication and answers 401; one that stands is projected by `SessionClaims.Project` onto the request's in-memory principal as the role, `permission`, `tenant_id`, `is_platform`, name and email claims the rest of the API reads. That projection is the only place a session becomes claims, so `Permissions(...)`, `ICurrentUserService`, `TenantContextProcessor` (which establishes the tenant from `tenant_id` alone, with no database read) and `GetInfoEndpoint` all see the stored session, never the token. A store that cannot be reached fails closed: `SessionStoreUnavailableMiddleware`, straight after `UseAuthentication()`, answers **503 `sessionStoreUnavailable`** — never 401, and never an anonymous pass. Sign-out deletes the caller's session as well as its refresh tokens, so the access token it signed out with answers 401 at once.

**An access change revokes the affected sessions at once**, through the `[AllowOutside]` `ISessionRevocationService` — the only way another slice ends a session; nothing outside Identity touches `ISessionStore`. It runs after the change commits (it refuses to run inside an open transaction, so a rolled-back change revokes nothing) and deletes the `AuthToken` refresh rows before the store records, so the old access token answers 401 on its next request and its refresh token is refused:

| Change | Sessions ended |
|---|---|
| A user deactivated or deleted, or their password reset | every session of that user |
| A caller changing their own password | every session of that user but the current one |
| A user's roles changed (`UserUpdateEndpoint`) | that user's sessions in the scope acted in |
| A role's permissions replaced, or the role deleted | its holders' sessions in the role's scope |
| A member removed from a tenant, or their tenant roles replaced | that user's sessions in that tenant |
| A tenant suspended or deleted, its edition changed, or its feature values changed | every session in that tenant |
| An edition's feature values changed, or the edition deleted | every session in every tenant on it |

A new endpoint that changes what a session may do — an account's activity, credentials or roles, a role's permissions, a membership, or a tenant's status or plan — must revoke the same way (profile fields such as username and email do not revoke; a session keeps the values it was created with until it is replaced), and a new point that issues a session must go through `ISessionIssuer` and call `TokenService.RecordSessionTenant` and `TokenService.RecordSessionId` with the `SecurityStamp` it was authorized under. Revocation is not atomic with the change: a store outage part-way through answers 503 with the change committed and the refresh rows gone, but the store records still standing, so the old access token works again once the store is back, until `Auth:AccessTokenValidity` runs out — a 503 there does not mean the change was not applied. What revokes nothing — `DataSeeder`'s startup reconciliation, the `FeatureManagement` configuration section — reaches a session when it is next replaced. Refresh still re-examines the account: `TokenService.SetRenewalPrivilegesAsync` refuses a deactivated account and a refresh token issued before the account's `SecurityStamp` last changed, and drops a tenant that has been suspended, deleted or left, renewing the session without one rather than ending it. `Auth:AccessTokenValidity` bounds the access token and the auth cookie alike — the cookie is configured with `SlidingExpiration = false` so a browser goes through that refresh on the same clock as a bearer client. Sign-in puts an ordinary account into exactly one tenant — its single active membership, or the `TenantIdentifier` it supplies — and refuses with `tenantRequired` otherwise; a platform account signs in with no tenant unless it names one. Every tenant a session enters — at sign-in, on `POST /tenants/switch` and at refresh — needs a live membership of it, whatever the account's tier; a platform account returns to platform scope through `POST /tenants/exit`. Refresh tokens and forgot-password tokens are cleaned by Hangfire recurring jobs registered at the end of `Program.cs`.

**Errors.** Endpoints call `this.ThrowError(ErrorCodes.X)`, naming only the code — never an English message, which would drift from what `error.server.<code>` ships and would have to be kept in step across every resource file by hand. `ErrorCodes` members (`ErrorHandling/ErrorCodes.cs`) are `public static readonly ErrorCode` values, `ErrorCode` (`ErrorHandling/ErrorCode.cs`) a `readonly record struct` wrapping the code string with deliberately no implicit conversion to `string` — read `.Value` where a plain string is actually required (a `switch` case, an attribute argument, `WithErrorCode(ErrorCodes.X.Value)`). The coded overloads are extensions on `EndpointExtension`, called through the `this.` receiver: FastEndpoints' `Endpoint<TRequest,TResponse>` declares its own `ThrowError` overloads, none taking an `ErrorCode`, so `this.ThrowError(ErrorCodes.X)` binds the extension, while a bare `ThrowError(ErrorCodes.X)` never considers extensions and does not compile. The missing implicit conversion to `string` is what keeps it that way — with one, FastEndpoints' `ThrowError(string message, int? statusCode)` would become applicable and win, sending the code as the message. FastEndpoints serializes ProblemDetails with the error code included, and `ExceptionProcessor` / `ToLargePayloadProcessor` / `UnsupportedMediaTypeResponseProcessor` are wired globally in `Program.cs`. `Program.cs` also installs ASP.NET's request-localization middleware ahead of `UseFastEndpoints` — `Accept-Language` only, supported cultures the shipped resource files, default `en` — and a business error, one whose code carries an `error.server.<code>` resource key (a `ThrowError` call, a validator rule declared with `.WithErrorCode(ErrorCodes.X.Value)`, `featureDisabled`/`featureLimitExceeded`, payload-too-large, the DB-error mapping, and the generic 500 outside Development), is served in that culture through `ErrorLocalization.LocalizeErrorAsync`, an extension over the narrow `IErrorMessageLocalizer`: `Endpoints.GlobalResponseModifierAsync` calls it to rewrite a ProblemDetails response's `errors[].reason` (and `detail`, where it mirrors one error), while `ExceptionProcessor` and `ToLargePayloadProcessor` call it directly for the responses they build by hand. `TenantContextProcessor` is registered ahead of `ToLargePayloadProcessor` precisely so a 413 already has a resolved tenant scope to read overrides from; the permission refusal `AuthorizationRefusalResultHandler` answers with runs inside `UseAuthorization`, ahead of every FastEndpoints pre-processor, so for that one case `LocalizeErrorAsync` falls back to reading the session's own `tenant_id` claim the same way `TenantContextProcessor` does (`TenantContextProcessor.ReadSessionTenantId`), used for nothing but choosing which override text applies. A failure resolving the message (the database being unreachable, say) is logged and answered with the shipped English text for the code, read straight off the resource store with no database involved — or, for a code with no resource key (a plain FluentValidation rule), the message as written — rather than turning a classified error into an unrelated 500. A message's `${propertyName}` is filled with that field's own localized label — a `Normalized` suffix stripped, first letter lowercased — falling back to the raw field name when no such label is shipped. A plain FluentValidation rule with no `ErrorCodes` constant behind it (`NotEmptyValidator`, …) has no resource key and is left exactly as FluentValidation wrote it; FluentValidation's own per-culture default messages (its `LanguageManager`) are deliberately left enabled rather than disabled, so such a rule still varies by the ambient `CurrentUICulture` the request-localization middleware sets, independently of this mechanism.

**Startup guards** worth knowing before changing configuration: the JWT key placeholder is rejected outside Development/Testing, a missing `ConnectionStrings:Redis` is rejected outside Testing (a Redis that is configured but down does not stop startup — requests answer 503 `sessionStoreUnavailable` until it is back), `Database:ApplyMigrationsOnStartup` defaults to true only in Development/Testing, and the `Payload`/`Web`/`Auth` options are `ValidateOnStart`. Under `Testing` the host additionally runs no Hangfire worker, logs at `Warning` and applies no request rate limit — defaults that live in `Program.cs` because `appsettings.Testing.json` is not in source control.

## Backend tests

xUnit v3 + `FastEndpoints.Testing`. `App : AppFixture<Program>` runs the host with environment `Testing` and is registered as an **assembly fixture** in `Tests/Meta.cs`, so it is built, migrated and seeded once for the whole run. `AppTestsBase` gives each test its own `Client` and its own DI scope — `DbContext`, `TenantContext` and `Service<T>()` all resolve from it — plus `SetAuthTokenAsync()` (defaults to the default tenant's administrator `tenantadmin` / `Admin#123`; `SetPlatformAdminAuthTokenAsync()` signs in as the platform administrator `admin`) and `CreateAdminUserAsync`. Tests call endpoints type-safely — `Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, UserCreateResponse>(request)` — assert with FluentAssertions, and build payloads with Bogus `Faker<T>`. Shared fixtures live in `Tests/Seeder` (`TestRoles`, `TestUsers`, `TestsDataSeeder`); reuse them instead of creating ad-hoc roles. **Test classes run in parallel**, one collection per class, so a test must not depend on global database state it did not create; a class that shares a resource with another names a `[Collection]` for itself (`Notifications`, `FileManagement`, `BootstrapTenant`, `FeatureManagement`, `Localization`). The test host substitutes a cheap password hasher, a mail service that sends nothing, and a wrapper over the in-memory session store that a test can make unreachable for sessions or accounts it created itself (`Tests/Fakes`); `SessionOfAsync(accessToken)` reads the session a token names, since the token holds nothing but `sid`. Under `Testing`, `Program.cs` also runs no Hangfire worker, logs at `Warning` and lifts the request rate limit.

## Frontend architecture

Next.js App Router under `app/[lang]/` — **every route is locale-prefixed**. Route groups: `(public)`, `(auth)` (signin/signup/profile/password flows), and `admin/`. Page components are server components that resolve `params`, fetch a title via `getServerTranslation(lang, key)`, and render an interactive client component from a sibling `_components/` folder inside `AdminPageContent`.

`proxy.ts` (Next 16's renamed middleware) does two things: locale negotiation and auth gating based on `auth-urls.ts` plus the auth cookie. The routable locales are `i18n/config.ts`; the default locale is rewritten, the others redirected, and the locale decision itself is the pure `decideLocaleRouting` in `i18n/locale-routing.ts`. A URL with no locale gets one from `i18n/resolve-locale.ts` — the `preferred-language` cookie → the `scope-language` cookie → `Accept-Language` → the default — counting only cultures in the `scope-languages` cookie (the acting scope's enabled set), and a URL whose locale is routable but not enabled is redirected to the resolved one, so the proxy and the API always agree on the served culture.

**Translations come from the API** (see **Localization** above). `getDictionary` in `i18n/server.ts` is a `cache()`d server fetch of `GET /localization/resources/{culture}` that forwards the request's cookies, so tenant and platform overrides resolve for the caller; it loads `next/headers` lazily so the `@/i18n` barrel stays safe for client components. Import every translator from `@/i18n`, never from a subpath. The root layout hands the whole response — served `culture`, `defaultCulture`, enabled `languages`, flat `resources` (`i18n/translate.ts` looks keys up and interpolates `${name}`) — to `TranslationProvider`; the language dropdown lists those languages and RTL follows the served language's `isRtl`. `LocaleGuard` (`components/layouts/locale-guard.tsx`, decision in `i18n/locale-guard.ts`) navigates to the served culture when the URL's locale is not enabled, or to the scope's default when the visitor has no `preferred-language` cookie, only ever targets a culture that is enabled and routable, keeps the `scope-language` (default) and `scope-languages` (enabled set) cookies in step, and clears a `preferred-language` cookie naming a culture the scope does not enable. `i18n/locale-routing-loop.test.ts` replays proxy → API → guard hops over a matrix of scopes and visitors and fails if any combination does not settle. Anything that changes the acting scope (sign-in, tenant switch/exit) calls `router.refresh()` so the layout re-fetches. Overrides and language settings are edited on `app/[lang]/admin/localization`.

**Data layer is RTK Query.** `store/api/_app-api.ts` defines the single `appApi` with a `baseQueryWithReauth` that serializes refresh attempts through an `async-mutex` and signs the user out (redirecting to `/signin?redirect=…`) when refresh fails. Feature APIs live in `store/api/<feature>/<area>/{<area>-api.ts,<area>-dtos.ts}` and attach via `appApi.enhanceEndpoints({ addTagTypes: [...] }).injectEndpoints(...)` — never call `createApi` again. DTO types mirror the backend request/response classes by name.

Client permission checks use the `Allow` map in `allow.ts`, kept in sync with the backend constants. Shared pieces: `components/{custom,ui,layouts,notifications}`, `hooks/` (`use-table-url-state`, `use-localized-router`, `use-debounce`, `use-notification-hub` — polling, not a socket), `lib/utils/` (API error helpers, auth helpers). Navigation and global search entries are declared in `nav-items.ts` and `searchable-items.ts`.

Adding a language means a resource file `Features/Localization/Core/Resources/<code>.json` with every English key, a `LanguageCatalog` entry, the code in `i18n/config.ts` and a flag at `public/assets/images/flags/<CODE>.svg`; `i18n/locales.test.ts` fails when `i18nConfig.locales` differs from the backend's resource files. If the project was created without multi-language support, only `en.json` is shipped and `i18n/config.ts` lists `en` alone.

## Spec-driven development

Work is described in `specs/` and built by an unattended loop. Saving a markdown file there is
starting development: the planner splits it into tasks in `.agent-queue/todo/` (with
`Depends-on:` ordering), and the runner builds them one at a time on the work branch — brief →
`claude -p` → `npm run verify` → required reviews → commit. `specs/TEMPLATE.md` is a worked example;
`specs/README.md` says what makes a brief work; `docs/AGENTIC_WORKFLOW.md` is the whole mechanism.
The loop reads diffs and commits, so the project must be a git repository (`git init`, then a first
commit) before it runs.

```sh
npm run queue -- add "<task>"       # queue one task directly
npm run queue -- plan               # turn new specs/*.md into tasks, without building
npm run queue -- drain              # build everything runnable, serially
npm run queue                       # what is waiting, blocked, done or failed
npm run auto -- "<task>"            # build one task now, outside the queue
npm run loop                        # preflight → observe → plan → drain → report
npm run schedule -- install         # run the loop on a timer (Task Scheduler / launchd)
npm run auto:status                 # every attempt, its turns, cost and why it failed
npm run lessons                     # what past runs recorded for future ones
npm run pr                          # the pull-request URL for the current branch
```

- **`agentic.config.json` is the one project-specific file.** It names the conventions every brief
  repeats, the departments (which agent owns which paths), the skills a diff owes, the gate and live
  checks, the service verify starts, and the hook rules. `scripts/lib/project-config.mjs` reads it;
  the rest of `scripts/` is stack-agnostic, except `gate.mjs`, `serve-api.mjs`, `pg-ready.mjs`,
  `redis-ready.mjs` and `smoke.mjs`, which are how this stack builds and runs.
- **Delegation is checked, not trusted.** The departments a change owes are derived from its finished
  diff and compared with the subagents actually seen in the run's stream: `ui-ux-reviewer` designs a
  screen first; `data-engineer`, `backend-engineer`, `frontend-engineer` build; `qa-engineer` and
  `security-reviewer` (when owned paths changed) review, then `code-reviewer`. The agents are in
  `.claude/agents/`.
- **The work branch** is `project.branch`, or whichever branch is checked out when that is `null`.
  The runner refuses a dirty tree, commits each task with a `Task: <brief>` trailer, and **never
  pushes unless `AGENT_AUTO_PUSH=1`** — and nothing here merges. Pull requests go to
  `project.baseBranch` (else `origin/HEAD`); `auto-ship` opens one with `gh` when it is installed.
- **Spend is bounded** by `AGENT_MAX_USD_PER_TASK` (default 50), `AGENT_MAX_USD_PER_DRAIN` (200) and
  `AGENT_MAX_RUNS_PER_TASK` (3); `AGENT_MODEL` picks the session model.
- **Guards run in every permission mode.** `.claude/hooks/` refuses reading or writing `.env*` and the
  per-environment `appsettings.*.json`, edits to build output, `dotnet ef database drop`, destructive
  SQL, `git reset --hard`, `git add -A`, force-pushes, pushes to a protected branch and every merge
  route — for the Bash and PowerShell tools alike. `npm run test:hooks` holds a block case and a
  neighbouring allow case for each rule; add both when you add a rule.
- **Records.** `.agent-runs/` (git-ignored) is every attempt; `docs/builds/` is one committed record
  per landed task; `.claude/memory/lessons/` is what runs learned, injected into later briefs.

## Task guides

`.claude/skills/` holds step-by-step guides for the recurring tasks in this codebase. Consult the matching one before writing code so new work follows the same shape as the existing features.

- Cross-cutting: `coding-conventions`
- API: `backend-feature`, `backend-endpoint`, `backend-entity`, `backend-tests`, `multi-tenancy`, `permissions`, `feature-management`, `background-jobs`, `file-storage`, `notifications`
- Web: `rtk-query-api`, `frontend-page`, `frontend-crud`, `ui-component`, `redux-state`, `localization`, `frontend-tests`
- Spanning both: `api-error-handling`
- Process: spec-driven development — `specs/README.md`, the `.claude/commands` (`/feature`, `/fix`, `/auto`, `/queue`, `/spec-split`, `/verify`, `/ship`, `/review-diff`), and the agents in `.claude/agents`
