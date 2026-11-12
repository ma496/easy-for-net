# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project layout

- `src/backend/Source` — ASP.NET 10 API built on FastEndpoints, EF Core (PostgreSQL) and Hangfire.
- `src/backend/Tests` — xUnit v3 integration tests that boot the real API host.
- `src/frontend/web` — Next.js 16 App Router frontend (React 19, Redux Toolkit Query, Tailwind): a public, server-rendered site at `/`, the visitor's account screens, and an admin area at `/admin`.

## Commands

Backend:

```sh
dotnet tool restore                 # restores dotnet-ef pinned in .config/dotnet-tools.json
dotnet build EasyForNet.slnx
dotnet ef migrations add <Name> --project src/backend/Source
dotnet ef database update --project src/backend/Source
dotnet run --project src/backend/Source   # needs PostgreSQL and Redis (ConnectionStrings:Redis)
npm run dev                              # from the repository root: the whole app — PostgreSQL + Redis (Docker), the API on :5000, then the web app (next dev on :3001) behind the dev proxy on :3000 once the API's /health answers; open http://localhost:3000 (the public site) and http://localhost:3000/admin (scripts/dev.mjs, scripts/dev-proxy.mjs; --no-docker, --api-only, --web-only)
npm run stop:api                         # from the repository root: stop this checkout's running API before a build (it locks bin/ on Windows)
docker compose up -d                     # from the repository root: PostgreSQL + Redis for development (docker-compose.yml)
npm run deploy:vps -- --host <ip> --domain <domain> --email <email>   # deploy to a VPS through Coolify: the site at <domain> (--www also serves www.<domain>, redirecting to it; DNS: an A record for each) (scripts/deploy-vps.mjs + scripts/deploy/vps/remote.sh, docker-compose.coolify.yml)
```

Backend tests — **require a running PostgreSQL** matching `appsettings.Testing.json` (not Redis: the Testing host keeps sessions in memory); the Testing environment migrates and seeds the database on startup:

```sh
dotnet test --project src/backend/Tests
dotnet test --project src/backend/Tests --filter-class "*UserCreateTests"
dotnet test --project src/backend/Tests --filter-method "*UserCreateTests.Valid_Input"
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
npm run gate -- --changed --plan    # which steps the working tree's changes reach, without running them
npm run verify -- --autostart       # the gate steps the diff reaches, plus the live API smoke check when src/backend/Source changed
npm run verify -- --full            # the same with every gate step
```

The API that `verify` starts for the live check waits on `scripts/pg-ready.mjs` and `scripts/redis-ready.mjs` (a TCP probe of `ConnectionStrings:Redis`), both listed under `verify.service.dependsOn` in `agentic.config.json`.

## Local sign-in (browser testing)

One account is seeded on first run. Locally (Development and Testing) it uses the password `Admin#123`:

| Account | What it is | Sign in at | Password |
|---|---|---|---|
| `admin` | the administrator (system-created, in the `Admin` role, which holds every permission) | `http://localhost:3000/admin` | `Admin#123` |

Visitors create their own accounts at `/signup`; each is given the role `General.SignupRoleName` names (`User`, seeded with no permission at all), so a signed-up visitor reaches the public site and their own account screens but not `/admin`. Sign-up is on by default (`General.IsSignupEnabled`); both are changed under Admin → Settings → General.

The password comes from `Seed:AdminPassword` in the appsettings files. It is applied only when the account is created, so changing it later in the app or in the settings won't change an existing account. Production seeds the account with the random `SEED_ADMIN_PASSWORD` from the root `.env`; never write that value anywhere. The API refuses to start on `Admin#123` outside Development/Testing.

## Backend architecture

**Vertical slices under `Features/`.** Each feature is `Features/<Feature>/{Core,Endpoints}`: `Identity`, `Settings`, `Localization`, `FileManagement`, `Notifications` and `Seo`.

- `Core` holds entities (`Core/Entities`, EF configuration in `Core/Entities/Configuration`), services, settings, and the feature's `IPermissionDefinitionProvider` (and `ISettingDefinitionProvider` when it owns a setting).
- `Endpoints/<Area>` holds one file per endpoint plus an `<Area>Group.cs` that owns the route prefix.
- `<Feature>Feature.cs` implements `IFeature.AddServices`. `Helper.AddFeatures` reflects over the assembly at startup and calls it — features are never registered by hand in `Program.cs`.

**One endpoint = one file.** `Features/Identity/Endpoints/Users/UserCreateEndpoint.cs` is the canonical shape: the `sealed class ...Endpoint : Endpoint<TReq, TRes>`, its `...Request`, a FluentValidation `...Validator : Validator<TReq>`, the `...Response`/DTOs, and the Riok.Mapperly `[Mapper] partial class ...Mapper` all live together. Types are internal where possible; `Meta.cs` grants `InternalsVisibleTo` to the test project so tests can reference them. Contracts several endpoints of one area share sit in one `<Area>Contracts.cs` (`Features/Seo/Endpoints/Pages/SeoPageContracts.cs`).

**Global usings live in `Meta.cs`** (FastEndpoints, FluentValidation, Mapperly, EF Core, the `ShareData`, `Base.Dto`, `Permissions` and `ErrorHandling` namespaces, …). Do not re-add those per file; add new project-wide ones there.

**Feature isolation is enforced by tests.** `Tests/Architect/FeatureDependencyTests` fails if a type under `<RootNamespace>.Features.X` depends on a type under `<RootNamespace>.Features.Y` unless that type is marked `[AllowOutside]`. `[NoDirectUse]` (with `[BypassNoDirectUse]` as the escape hatch) enforces consuming a class through its interface. `Tests/Architect/Features/Feature{A,B}` are fixtures that exercise the rules themselves — not real features. A slice that must react to another's event without depending on it implements the other's published handler interface (`ISettingChangedHandler`, `ISessionEndedHandler`).

**Permissions** are string constants in `Permissions/Allow.cs`, declared as a hierarchy by each feature's `IPermissionDefinitionProvider`, enforced on endpoints via `Permissions(Allow.X)`, and reconciled into the database by `ShareData/DataSeeder` on every startup (adds/renames/deletes rows, strips deleted permissions from roles, and keeps the system-created `Admin` role holding the whole catalogue). Adding a permission means: constant in `Allow.cs` → definition in the provider → mirror the constant in `src/frontend/web/allow.ts` (`allow.test.ts` pins the whole list). **Permissions are the only authorization input** — never a role name. A session's permissions are the union of what its account's roles grant (`SessionGrants`).

`Admin.Access` (declared with `Hangfire.Dashboard` by `Permissions/AdministrationPermissionsProvider.cs`) is what separates an administrator from an ordinary signed-in visitor, **on the web**: the web app opens `/admin` only to a session holding it. The API never checks it — every endpoint an admin screen calls declares its own permission (`User.View`, `Seo.Update`, …). Because sign-up is open, an endpoint with no permission is reachable by every registered visitor, which is right only for the caller's own data (profile, own notifications, own files), keyed by the session's account. Granting the signup role a permission grants it to every future visitor.

**Settings** answer a different question from permissions: not *may this caller do it* but *how does it behave here* — a typed value an administrator may change at run time. The system is the `Settings` slice (`Features/Settings`); what other slices use — `ISettingDefinitionProvider`, `SettingDefinitionContext`, `SettingDefinitionBuilder<T>`, `SecretSettingAttribute`, `ISettingProvider` and `ISettingChangedHandler` — is published with `[AllowOutside]`, and everything that resolves or stores a value stays private to it. A setting is a plain class whose read-write properties are its values and whose initializers are its code default, with a FluentValidation validator beside it; each slice registers its settings in `Core/<X>SettingsProvider.cs` (`context.Add<T>(name, validator)`), discovered by reflection. Chaining `.FromConfiguration("Section")` makes that configuration section, bound onto `new T()` once at startup, the default whenever the deployment supplies it — `EmailSettings` (`External/Email`, setting `Email`) is defaulted from the `EmailSettings` section this way. A value resolves **property by property** through two layers: the stored override (a single `SettingValue` row per setting, holding only the properties set) → the default; each property's source is `override` or `default`. A duplicate name, a class registered twice, an unbindable section or a default its own validator rejects stops startup. A `[SecretSetting]` property is a string stored encrypted with ASP.NET Data Protection, never returned by the API (only `isSet`), and — when it names the properties it is bound to — resolves to `""` wherever one of them is overridden without the secret being set too, so repointing a server does not hand it the configured password. A resolved setting carries its secrets in plaintext, so it is never returned from another endpoint, logged, put in an exception message, or passed as a job argument; a setting naming where the server connects must be validated against internal addresses. Code reads a setting through `ISettingProvider.GetAsync<T>()`, anywhere — a request, anonymous or not, or a job. `GET /settings`, `PUT /settings/{name}` and `DELETE /settings/{name}` list every declared setting with each property's source and replace or remove its override, under `Settings.View` / `Settings.Update`; after a change is saved every registered `ISettingChangedHandler` is told (`SeoCache` drops itself when `Seo` changes). `app/[lang]/admin/settings` edits them, one tab per setting it knows (`General`, `Email`, `Seo`); small cross-cutting options share the `General` setting (`GeneralSettings`: `IsEmailVerificationRequired`, `IsSignupEnabled`, `SignupRoleName`). A settings change revokes no session: what sign-in or refresh decides from one reaches a session when it is next replaced. Test classes that write a stored row share the `Settings` collection through `SettingsTestsBase`, which removes the rows after each test; a test that only needs a value for its own requests overlays it on its client (`OverlaySetting`, `AllowSignup`).

**Localization.** Translations are served by the API, not bundled with the web app. The shipped strings are nested JSON files in `Features/Localization/Core/Resources/<code>.json`, embedded in the assembly and flattened to dotted keys (`common.save`) once by `LocalizationResourceStore`; `LanguageCatalog` holds each code's display name and `isRtl`. On top of them sit two tables: `LocalizationText` overrides one key in one culture, and a single `LanguageSetting` row holds the enabled cultures and the default. A text resolves first answer wins: the stored override → the shipped value for the culture → the shipped English value. Languages resolve as a unit: the `LanguageSetting` row → every shipped culture enabled with no default. The culture served is the one requested if enabled, else the default, else `en`, else the first enabled. The `/localization` endpoints go through `ILocalizationService`, feature-internal like every other slice service: `GET resources/{culture}` is anonymous and answers every caller the same, and `GET/PUT/DELETE texts` and `GET/PUT/DELETE languages` edit the overrides under `Localization.View` / `Localization.Update`. Other slices use the published `ICultureCatalog` (canonical shipped cultures) and `ILanguageProvider` (the enabled set). `Tests/Features/Localization/Core/LocalizationResourceStoreTests` keeps every shipped file on English's exact key set, with no empty value, no value equal to its key, and a `LanguageCatalog` entry per file. The same resolution, through the narrow `[AllowOutside]` `IErrorMessageLocalizer`, is what puts a coded API error's message into the request's own culture — see Errors below.

**Seo.** The `Seo` slice (`Features/Seo`) holds what each public page says to search engines and social networks. A `SeoPage` (`seo.SeoPages`, soft-deleted) is keyed by a locale-free `Path` (`/`, `/about`, or a template such as `/blog/{slug}` whose whole-segment placeholders stand for any one segment) and an optional `Culture` (null for every language), with one live row per (path, culture) — a unique filtered index with nulls not distinct, answered first by `seoPageAlreadyExists`. It carries title (before the site's title template), description, keywords, canonical URL, Open Graph title/description/image (`OgImageFileId`, a stored file), Twitter card type, `NoIndex`/`NoFollow`, `IncludeInSitemap`, change frequency, priority and JSON-LD (a JSON object or array, `invalidJsonLd` otherwise). Site-wide defaults are the `Seo` setting (`SeoSettings`: site name, title template with `%s`, default description and image, Twitter handle, `SiteUrl` — empty falls back to `Web:DefaultDomain` — `AllowIndexing`, `RobotsExtra`), read through `ISeoSiteService`. Four anonymous endpoints serve the public site: `GET /seo/resolve?path=&culture=` finds the rows by exact path, else by the most specific matching template, and merges each field first answer wins from the culture's row → the culture-neutral row → the setting (`Index` false when indexing is off or the page says `NoIndex`; every URL absolute); `GET /seo/sitemap` lists each non-template path with a live row that is in the sitemap and not `noindex` (nothing when indexing is off), with the enabled cultures; `GET /seo/robots` answers the site URL, `AllowIndexing` and the extra lines; `GET /seo/images/{fileId}` streams a file only while a live page or the setting names it, and 404s anything else, so it never becomes a way to read the application's files. What resolve reads is cached in process per shipped culture (`ISeoCache`, at most 10 minutes); every page write calls `seoCache.Invalidate()`, and `SeoCache` is the `ISettingChangedHandler` that invalidates when `Seo` changes. The admin endpoints `GET/POST /seo/pages`, `GET/PUT/DELETE /seo/pages/{id}` check `Seo.View` / `Seo.Update`. `DataSeeder` inserts culture-neutral rows for `/`, `/about` and `/contact` once, never bringing back one an administrator deleted. See the `seo` skill.

**Data access.** `AppDbContext` applies entity configurations from the assembly, installs a global soft-delete query filter for `ISoftDelete`, and fills audit/normalized properties on save. List endpoints take a `ListRequestDto<TId>` and call `IQueryableExtension.Process(request)` for sorting/paging; sortable fields must be whitelisted in the request validator. A read-only query is `AsNoTracking()` and projects with `Select` to the columns it uses rather than loading entities with `Include`; every `Skip`/`Take` follows an `OrderBy` ending on a unique column (`Process` adds `Id` as tie-breaker), and in Development/Testing an unordered row limit throws (`RowLimitingOperationWithoutOrderByWarning`, configured in `Program.cs`). Every baseline row lives in `DataSeeder`, never in a migration, and each step is idempotent.

**Files.** `IFileService` (`[AllowOutside]`) records every upload as a `StoredFile`: an application file (no owner) is readable by any signed-in caller, an account-owned one (`accountOwned: true`, `OwnerUserId`) by its owner alone — so anything personal a visitor uploads is account-owned. `OpenPublicAsync(fileId)` opens a file with no check at all and is called only from an anonymous endpoint that serves files it references itself (the SEO image route). `Payload:MaximumSize` is the one upload limit. See the `file-storage` skill.

**Auth.** A `Jwt_Or_Cookie` policy scheme picks JWT bearer when an `Authorization: Bearer` header is present, cookies otherwise. **A token or auth cookie carries only who the caller is (`ClaimTypes.NameIdentifier`) and which session (`sid`)**; everything else — username, email, roles, permissions — is a `SessionRecord` in `ISessionStore` (`Features/Identity/Core/Sessions`). The store is Redis (`ConnectionStrings:Redis`, every key prefixed with `Redis:InstanceName`) in every environment but `Testing`, where one in-memory store is shared by the whole run so the tests need only PostgreSQL. Sign-in and refresh go through `ISessionIssuer`, which computes the session with `SessionGrants` and writes it to the store for `Auth:RefreshTokenValidity`; refresh deletes the session it replaces. Sign-in refuses an inactive account (`userNotActive`) and, while `General.IsEmailVerificationRequired` is on, an unverified one (`emailNotVerified`).

**The notification hub** (`/hubs/notifications`, `Features/Notifications/Core/Push`) is behind the same authentication and session validation: its upgrade request answers 401 exactly when an HTTP request with the same credential would, and 503 `sessionStoreUnavailable` when the store is down. A browser authenticates it with the auth cookie; a bearer client passes `?access_token=`, which the policy scheme and the bearer handler honour on the hub's path alone (anywhere else the parameter is ignored), and which is kept out of the logs by pinning `Microsoft.AspNetCore.Hosting.Diagnostics` to `Warning` in `Program.cs`. An upgrade whose `Origin` is neither the request's own origin (its scheme and host) nor one of the `Web` domains (the list the CORS policy allows) is refused with 403 by `NotificationHubOriginMiddleware`; one with no `Origin` (a non-browser client) is let through. It is server-to-client only and WebSockets only; a notification is addressed to one account or to every user. See the `notifications` skill.

**Every authenticated request reads its session.** `SessionAuthentication` chains `SessionPrincipalValidator` onto both handlers' events, so it runs after the token or cookie authenticates and before authorization. It loads the session `sid` names: one that is missing, expired or belongs to another account than `NameIdentifier` fails authentication and answers 401; one that stands is projected by `SessionClaims.Project` onto the request's in-memory principal as the role, `permission`, name and email claims the rest of the API reads. That projection is the only place a session becomes claims, so `Permissions(...)`, `ICurrentUserService`, the Hangfire dashboard filter and `GetInfoEndpoint` all see the stored session, never the token. A store that cannot be reached fails closed: `SessionStoreUnavailableMiddleware`, straight after `UseAuthentication()`, answers **503 `sessionStoreUnavailable`** — never 401, and never an anonymous pass. Sign-out deletes the caller's session as well as its refresh tokens, so the access token it signed out with answers 401 at once.

**An access change revokes the affected sessions at once**, through the `[AllowOutside]` `ISessionRevocationService` (`RevokeUserAsync`, `RevokeUsersAsync`, `RevokeUserExceptAsync`) — the only way another slice ends a session; nothing outside Identity touches `ISessionStore`. It runs after the change commits (it refuses to run inside an open transaction, so a rolled-back change revokes nothing) and deletes the `AuthToken` refresh rows before the store records, so the old access token answers 401 on its next request and its refresh token is refused:

| Change | Sessions ended |
|---|---|
| A user deactivated or deleted, or their password reset | every session of that user |
| A caller changing their own password | every session of that user but the current one |
| A user's roles changed (`UserUpdateEndpoint`) | every session of that user |
| A role's permissions replaced, or the role deleted | every session of each of its holders |

**An ended session also closes its hub connections.** Every deletion of a session record — each row above, sign-out, and the session replaced by refresh — goes through the `ISessionStore` that `PublishingSessionStore` wraps, which, once the store has deleted it, announces the ended `sid`s to every `[AllowOutside]` `ISessionEndedHandler` on every API instance: over Redis pub/sub on `ConnectionStrings:Redis` (channel prefixed with `Redis:InstanceName`) outside `Testing`, in process under it. Notifications' handler aborts every hub connection that `sid` authenticated. An announcement that fails is logged and never fails the sign-out, refresh or revocation that made it.

A new endpoint that changes what a session may do — an account's activity, credentials or roles, or a role's permissions — must revoke the same way (profile fields such as username and email do not revoke; a session keeps the values it was created with until it is replaced), and a new point that issues a session must go through `ISessionIssuer` and call `TokenService.RecordSessionId` with the `SecurityStamp` it was authorized under. Revocation is not atomic with the change: a store outage part-way through answers 503 with the change committed and the refresh rows gone, but the store records still standing, so the old access token works again once the store is back, until `Auth:AccessTokenValidity` runs out — a 503 there does not mean the change was not applied. What revokes nothing — `DataSeeder`'s startup reconciliation, a settings change — reaches a session when it is next replaced. Refresh still re-examines the account: `TokenService.SetRenewalPrivilegesAsync` refuses a deactivated account, a refresh token issued before the account's `SecurityStamp` last changed, and (while verification is required) an unverified email. `Auth:AccessTokenValidity` bounds the access token and the auth cookie alike — the cookie is configured with `SlidingExpiration = false` so a browser goes through that refresh on the same clock as a bearer client. Sign-up (`POST /account/signup`) is open while `General.IsSignupEnabled` is on — refused otherwise with 403 `signupDisabled` before anything is looked up — and creates the account, its signup role (when a role of that name exists; otherwise no role) and, while verification is required, its verification token in one transaction, enqueuing the verification email only after the commit. Emailed links (verification, password reset) are built from `Web:DefaultDomain`. Refresh tokens, forgot-password tokens and notifications past `Notifications:RetentionDays` are cleaned by Hangfire recurring jobs registered at the end of `Program.cs`. The Hangfire dashboard (`/hangfire`) admits a session holding `Hangfire.Dashboard` (`HangfireAuthorizationFilter`).

**Errors.** Endpoints call `this.ThrowError(ErrorCodes.X)`, naming only the code — never an English message, which would drift from what `error.server.<code>` ships and would have to be kept in step across every resource file by hand. `ErrorCodes` members (`ErrorHandling/ErrorCodes.cs`) are `public static readonly ErrorCode` values, `ErrorCode` (`ErrorHandling/ErrorCode.cs`) a `readonly record struct` wrapping the code string with deliberately no implicit conversion to `string` — read `.Value` where a plain string is actually required (a `switch` case, an attribute argument, `WithErrorCode(ErrorCodes.X.Value)`). The coded overloads are extensions on `EndpointExtension`, called through the `this.` receiver: FastEndpoints' `Endpoint<TRequest,TResponse>` declares its own `ThrowError` overloads, none taking an `ErrorCode`, so `this.ThrowError(ErrorCodes.X)` binds the extension, while a bare `ThrowError(ErrorCodes.X)` never considers extensions and does not compile. The missing implicit conversion to `string` is what keeps it that way — with one, FastEndpoints' `ThrowError(string message, int? statusCode)` would become applicable and win, sending the code as the message. FastEndpoints serializes ProblemDetails with the error code included, and `ExceptionProcessor` / `ToLargePayloadProcessor` / `UnsupportedMediaTypeResponseProcessor` are wired globally in `Program.cs`. `Program.cs` also installs ASP.NET's request-localization middleware ahead of `UseFastEndpoints` — `Accept-Language` only, supported cultures the shipped resource files, default `en` — and a business error, one whose code carries an `error.server.<code>` resource key (a `ThrowError` call, a validator rule declared with `.WithErrorCode(ErrorCodes.X.Value)`, payload-too-large, the DB-error mapping, the 401/403 `AuthorizationRefusalResultHandler` answers, and the generic 500 outside Development), is served in that culture through `ErrorLocalization.LocalizeErrorAsync`, an extension over the narrow `IErrorMessageLocalizer`: `LocalizingResponseSerializer` (installed as `Serializer.ResponseSerializer`) calls it to rewrite a ProblemDetails response's `errors[].reason` (and `detail`, where it mirrors one error), while `ExceptionProcessor` and `ToLargePayloadProcessor` call it directly for the responses they build by hand. A failure resolving the message (the database being unreachable, say) is logged and answered with the shipped English text for the code, read straight off the resource store with no database involved — or, for a code with no resource key (a plain FluentValidation rule), the message as written — rather than turning a classified error into an unrelated 500. A message's `${propertyName}` is filled with that field's own localized label — a `Normalized` suffix stripped, first letter lowercased — falling back to the raw field name when no such label is shipped. A plain FluentValidation rule with no `ErrorCodes` constant behind it (`NotEmptyValidator`, …) has no resource key and is left exactly as FluentValidation wrote it; FluentValidation's own per-culture default messages are deliberately left enabled, so such a rule still varies by the ambient `CurrentUICulture` the request-localization middleware sets. `Tests/ErrorHandling/ErrorCodeCatalogTests` holds every code to a shipped English text.

**Startup guards** worth knowing before changing configuration: the JWT key placeholder and the local seed password `Admin#123` (`SeedSetting.LocalPassword`, in `Seed:AdminPassword`) are rejected outside Development/Testing, a missing `ConnectionStrings:Redis` is rejected outside Testing (a Redis that is configured but down does not stop startup — requests answer 503 `sessionStoreUnavailable` until it is back), `Database:ApplyMigrationsOnStartup` defaults to true only in Development/Testing, and the `Payload`/`Web`/`Auth`/`Notifications`/`Seed` options are `ValidateOnStart` (`Web:DefaultDomain` and `Web:Domains` must be absolute URLs; `Seed:AdminPassword` must be 8 to 50 characters; `Notifications:RetentionDays`, default 90, and `Notifications:MaxConnectionsPerUser`, default 20 hub connections per account per API instance, must both be greater than zero). Behind a reverse proxy in another container, `ForwardedHeaders:TrustAllProxies` makes the API honour its `X-Forwarded-*` headers (`For`, `Proto` and `Host`) (only loopback proxies are trusted otherwise), and `DataProtection:KeysPath` persists the Data Protection key ring — which encrypts `[SecretSetting]` values — to a directory that must outlive the container; `docker-compose.prod.yml` sets both. The notification hub scales out through a Redis backplane on `ConnectionStrings:Redis`, its channels prefixed with `Redis:InstanceName`. Under `Testing` the host additionally runs no Hangfire worker and no hub backplane (hub pushes stay in process), logs at `Warning` and applies no request rate limit — defaults that live in `Program.cs` because `appsettings.Testing.json` is not in source control.

## Backend tests

xUnit v3 + `FastEndpoints.Testing`. `App : AppFixture<Program>` runs the host with environment `Testing` and is registered as an **assembly fixture** in `Tests/Meta.cs`, so it is built, migrated and seeded once for the whole run. `AppTestsBase` gives each test its own `Client` and its own DI scope — `DbContext` and `Service<T>()` resolve from it — plus `SetAuthTokenAsync()` (defaults to the seeded administrator `admin`, signing in with the `Seed:AdminPassword` configuration value the seeder used, exposed as `TestUsers.AdminPassword`), `CreateAdminUserAsync`, `OverlaySetting` / `AllowSignup` / `SignupVisitorClient` (a setting value for one client's requests alone, never a stored row) and `SessionOfAsync(accessToken)`, which reads the session a token names, since the token holds nothing but `sid`. Tests call endpoints type-safely — `Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, UserCreateResponse>(request)` — assert with FluentAssertions, and build payloads with Bogus `Faker<T>`. Shared fixtures live in `Tests/Seeder` (`TestRoles`, `TestUsers`, `TestsDataSeeder`: an administrator, a limited account and a plain account with the signup role); reuse them instead of creating ad-hoc roles. Anonymous endpoints are tested with a client that has no credentials at all (`SeoTestsBase.AnonymousClient()`). **Test classes run in parallel**, one collection per class, so a test must not depend on global database state it did not create; a class that shares a resource with another names a `[Collection]` for itself (`Notifications`, `FileManagement`, `Localization`, `Settings` — the SEO suite joins `Settings`, since the public endpoints read the `Seo` setting through one process-wide cache). The test host substitutes a cheap password hasher, a mail transport that sends nothing but records each message (`RecordingEmailTransport`), a test-only probe setting, per-client setting overlays, a storage wrapper that records every saved file so the run can remove what its tests left (each test deletes its own uploads with `DeleteAfterTest`), and a wrapper over the in-memory session store that a test can make unreachable for sessions or accounts it created itself (`Tests/Fakes`). Under `Testing`, `Program.cs` also runs no Hangfire worker and no hub backplane, logs at `Warning` and lifts the request rate limit.

## Frontend architecture

Next.js App Router under `app/[lang]/` — **every route is locale-prefixed**. Three areas:

- `(site)` — the **public site**: `/` (home), `/about`, `/contact`, under a header and footer of their own (`(site)/_components/site-header.tsx` holds the page links). Pages are server components rendered with their content in the first HTML; nothing waits on the visitor's session (the header fills in the account menu once the account info answers).
- `(account)` — the account flows, adding no URL segment: the anonymous screens (`/signin`, `/signup`, `/forget-password`, `/reset-password`, `/verify-email`, `/unauthorized`, in `AuthShell`) and the signed-in visitor's own screens under `/account` (`/account/profile`, `/account/change-password`, in `AccountShell`).
- `admin` — the **admin area** at `/admin` (dashboard at its root; `admin/(identity)/users`, `admin/(identity)/roles`, `admin/seo`, `admin/settings`, `admin/localization`, `admin/notifications`), inside the sidebar-and-header shell.

Addresses of the last two are built with `adminPath(...)` and `accountPath(...)` (`lib/paths.ts`), never written by hand. An admin or account page component is a server component that resolves `params`, fetches a title via `getServerTranslation(lang, key)`, and renders an interactive client component from a sibling `_components/` folder inside `PageContent`. The admin and account layouts export `robots: { index: false, follow: false }`.

**Who may open what** is decided in three layers. `proxy.ts` (Next 16's renamed middleware) does locale negotiation and requires the auth cookie only for `/admin` and `/account` (`isAuthRequired` in `auth-urls.ts`), redirecting to `/signin?redirect=…`; every other page is public. `authUrls` in `auth-urls.ts` maps each guarded screen to the permissions it needs — every admin entry through its `admin(...)` helper, which puts `Admin.Access` in front of the screen's own permission, and an admin path no entry names falls back to `Admin.Access` alone. `AuthGuard` (`components/layouts/auth-guard.tsx`, decision in `guard-redirect.ts`) wraps only the admin layout and the account layout: it shows the loader until the account info has answered, the service-unavailable view when the API is down, and sends a visitor without a session to sign in and one without the permissions to `/unauthorized`. The public site never renders it. After sign-in a visitor lands on their `redirect`, else `/admin` when they hold `Admin.Access`, else `/` (`resolveSigninLanding` in `lib/utils/redirect.ts`). `anonymousUrls` names the screens that only make sense without a session.

**Public pages and SEO.** A public page calls `buildMetadata(lang, path)` from its `generateMetadata` and renders `<JsonLd lang path />` (`lib/server/seo.tsx`): both read `GET /seo/resolve` once per request (`resolveSeo` is `cache()`d) and `toMetadata` (`lib/seo/metadata.ts`, pure) maps the answer onto Next's `Metadata` — an absolute title through the site's template, description, keywords, the canonical address and one hreflang alternate per served culture plus `x-default` (the default culture unprefixed), Open Graph and X cards, robots directives. When the API cannot be reached the page still renders, titled with the brand. A page never hard-codes its title or description: those are its `SeoPage` row, edited at `/admin/seo`. `app/sitemap.ts` and `app/robots.txt/route.ts` are rendered on request from `GET /seo/sitemap` and `GET /seo/robots` (`lib/seo/sitemap.ts`, `lib/seo/robots.ts`); robots always disallows `/admin`, `/account` and `/api`, and disallows everything when the API cannot be asked. The `/seo` fetches send no cookies and no forwarded host and are cached for five minutes under the `seo` tag. After an admin save that changes a public page, the screen calls `revalidatePublicCache(['seo' | 'localization'])` (`lib/revalidate-client.ts`), which posts to `app/[lang]/admin/revalidate/route.ts`; that route asks `GET /account/get-info` with the caller's cookies and clears a tag only for a caller holding its permission (`decideRevalidation` in `lib/revalidate-rule.ts`: `seo` → `Seo.Update`, `localization` → `Localization.Update`). See the `seo` skill.

**Translations come from the API** (see **Localization** above). `getDictionary` in `i18n/server.ts` is a `cache()`d server fetch of `GET /localization/resources/{culture}`: for a visitor with no session cookie it sends no cookies and is cached for five minutes under the `localization` tag (the same answer for every anonymous visitor, so public pages stay cheap), for a signed-in visitor it forwards the cookies and is never cached (`dictionaryFetchInit`); when the API cannot be reached it falls back to an empty dictionary. A missing key translates to `''`, so **every translator call carries its English default after `||`** — `t('common.save') || 'Save'`, `(await getServerTranslation(lang, 'brand.name')) || 'Easy For Net'`, a key known only at run time `|| humanizeKey(value)` — and that default is what renders when the API is down; `i18n/default-texts.test.ts` fails when a literal-key call has no default or a plain-string default differs from `en.json`. It loads `next/headers` lazily so the `@/i18n` barrel stays safe for client components. Import every translator from `@/i18n`, never from a subpath. Public page copy lives under `site.<page>.*`, admin and account copy under `page.<area>.*`. The root layout hands the whole response — served `culture`, `defaultCulture`, enabled `languages`, flat `resources` (`i18n/translate.ts` looks keys up and interpolates `${name}`) — to `TranslationProvider`; the language dropdown lists those languages and RTL follows the served language's `isRtl` from the first HTML. The routable locales are `i18n/config.ts`; the default locale is rewritten, the others redirected, and the locale decision itself is the pure `decideLocaleRouting` in `i18n/locale-routing.ts`. A URL with no locale gets one from `i18n/resolve-locale.ts` — the `preferred-language` cookie → the `scope-language` cookie (the site's default) → `Accept-Language` → the default — counting only cultures in the `scope-languages` cookie (the enabled set), and a URL whose locale is routable but not enabled is redirected to the resolved one, so the proxy and the API always agree on the served culture. `LocaleGuard` (`components/layouts/locale-guard.tsx`, decision in `i18n/locale-guard.ts`) navigates to the served culture when the URL's locale is not enabled, or to the default when the visitor has no `preferred-language` cookie, only ever targets a culture that is enabled and routable, and keeps those cookies in step. `i18n/locale-routing-loop.test.ts` replays proxy → API → guard hops over a matrix of settings and visitors and fails if any combination does not settle. Overrides and language settings are edited on `app/[lang]/admin/localization`, which revalidates the `localization` tag after each save.

**Data layer is RTK Query.** `store/api/_app-api.ts` defines the single `appApi` with a `baseQueryWithReauth` that serializes refresh attempts through an `async-mutex` and, when refresh fails, signs the user out — redirecting to `/signin?redirect=…` only from a page that requires a session (`store/signed-out-navigation.ts`); on a public page the visitor simply stays, signed out. Feature APIs live in `store/api/<feature>/<area>/{<area>-api.ts,<area>-dtos.ts}` and attach via `appApi.enhanceEndpoints({ addTagTypes: [...] }).injectEndpoints(...)` — never call `createApi` again. DTO types mirror the backend request/response classes by name. Public pages use no RTK Query: they read the API through server fetches.

**The browser reaches the API same-origin, at `/api`** (`environment.apiUrl`; the development proxy and the production reverse proxy route `/api`, `/hubs`, `/hangfire` and `/health` to the API), so the auth cookie is first-party; the Next.js server's own fetches go through `serverApiFetch` (`lib/server/api-fetch.ts`) to `API_INTERNAL_URL`, forwarding the visitor's host as `X-Forwarded-Host` and their cookies only when asked (`forwardHost`, `forwardCookies`). A cached server fetch never forwards cookies.

Client permission checks use the `Allow` map in `allow.ts`, kept in sync with the backend constants (`isAllowed(authState, [Allow.X])`). Shared pieces: `components/{custom,ui,layouts,notifications}`, `hooks/` (`use-table-url-state`, `use-localized-router`, `use-debounce`, `use-notification-hub` — one SignalR connection to the notification hub from the admin shell's header, polling the unread count only while it is down), `lib/utils/` (API error helpers, auth helpers). The admin area's navigation and global search entries are declared in `nav-items.ts` and `searchable-items.ts`; the public site's links in its header and footer.

Adding a language means a resource file `Features/Localization/Core/Resources/<code>.json` with every English key, a `LanguageCatalog` entry, the code in `i18n/config.ts` and a flag at `public/assets/images/flags/<CODE>.svg`; `i18n/locales.test.ts` fails when `i18nConfig.locales` differs from the backend's resource files. If the project was created without multi-language support, only `en.json` is shipped, and `i18n/config.ts` lists `en` alone.

## Spec-driven development

Work is described in `specs/` and built by an unattended loop. Saving a markdown file there is
starting development: the planner splits it into tasks in `.agent-queue/todo/` (with
`Depends-on:` ordering), and the runner builds them one at a time on the work branch — brief →
`claude -p` → `npm run verify` → required reviews → commit. `specs/TEMPLATE.md` is a worked example;
`specs/README.md` says what makes a brief work; `docs/AGENTIC_WORKFLOW.md` is the whole mechanism.
The loop reads diffs and commits, so the project must be a git repository; it is created as one with a
first commit, and when git was missing then, run `git init` and make that first commit before the loop runs.

```sh
npm run queue -- add "<task>"       # queue one task directly
npm run queue -- plan               # turn new specs/*.md into tasks, without building
npm run queue -- drain              # build everything runnable, serially
npm run queue                       # what is waiting, blocked, done or failed
npm run auto -- "<task>"            # build one task now, outside the queue
npm run loop                        # preflight → observe → plan → drain → report
npm run schedule -- install         # run the loop on a timer (Task Scheduler / launchd)
npm run auto:status                 # every attempt, its turns, cost and why it failed
npm run monitor                     # read-only web page: tasks by lane per spec, live stream of the run (127.0.0.1:4000)
npm run lessons                     # what past runs recorded for future ones
npm run owes                        # which reviews and skills the working tree's change owes (the runner's rule)
npm run pr                          # the pull-request URL for the current branch
npm run test:claude-contract        # check the installed Claude CLI still emits what the runner reads
```

- **`agentic.config.json` is the one project-specific file.** It names the conventions every brief
  repeats, the departments (which agent owns which paths), the skills a diff owes, the gate and live
  checks, the service verify starts, and the hook rules. `scripts/lib/project-config.mjs` reads it;
  the rest of `scripts/` is stack-agnostic, except `gate.mjs`, `serve-api.mjs`, `stop-api.mjs`, `pg-ready.mjs`,
  `redis-ready.mjs` and `smoke.mjs`, which are how this stack builds and runs.
- **Delegation is checked, not trusted.** The departments a change owes are derived from its finished
  diff and compared with the subagents actually seen in the run's stream: `ui-ux-reviewer` designs a
  screen first; `data-engineer`, `backend-engineer`, `frontend-engineer` build; `qa-engineer`,
  `security-reviewer` (when owned paths changed) and `code-reviewer` (unless the diff is markdown
  outside `.claude/` alone, `exceptWhenOnly`) review together, in one tier. A retry keeps the design and build
  delegations of the run's earlier attempts, since their work is still in the tree; a review counts
  only in the attempt it judged. The agents are in `.claude/agents/`.
- **Solo or team** is `project.workflow`. `solo` (the default) builds on whatever branch is checked
  out, the base branch included. `team` never plans or builds on the base branch — `queue plan`/`drain`,
  `loop`, `auto` and `schedule install` refuse there — so each spec is written, planned and built on a
  branch of its own and reaches the base through a pull request. `specs/`, `.agent-queue/todo/` and
  `.agent-queue/planned/` are tracked (planning commits them as `Plan <spec>`); `doing/`, `done/` and `failed/`
  are per machine and gitignored. A task has landed when a commit reachable from HEAD names it on a
  `Task:` line, which survives a rebase or squash merge. Each spec's tasks live in a folder named for
  the spec (`adhoc` for hand-queued tasks) and a task's identity is `<spec>/<name>`, so every spec
  numbers from `01-`; planning an edited spec again numbers on past the names it already used, and a
  planning call that writes outside its folder or reuses a name queues nothing.
- **The work branch** is `project.branch`, or whichever branch is checked out when that is `null`.
  The runner refuses a dirty tree that is not its own salvage (`--salvage` takes it over), a live
  drain and a half-finished git operation, commits each task with a `Task: <brief>` trailer, and
  **never pushes unless `AGENT_AUTO_PUSH=1`** — and nothing here merges. Pull requests go to
  `project.baseBranch` (else `origin/HEAD`); `npm run pr` prints the URL, and
  `npm run auto:ship -- "<subject>" --push` pushes a non-protected branch and opens one with `gh`.
  In `team`, a drain builds only the briefs its own branch added, and the guard refuses a commit
  on the base branch.
- **Spend is bounded** by `AGENT_MAX_USD_PER_TASK` (default 50), `AGENT_MAX_USD_PER_DRAIN` (200) and
  `AGENT_MAX_RUNS_PER_TASK` (3). Each attempt is also stopped by the CLI itself at `budget.maxTurns`
  parent turns (`--max-turns`) and at what is left of the task's ceiling (`--max-budget-usd`), and
  after `AGENT_MAX_MINUTES_PER_ATTEMPT` (120) of wall clock; no attempt starts once the task has run
  `AGENT_MAX_MINUTES_PER_TASK` (240). A stopped session is killed with everything below it, and the
  runner stops waiting on pipes an orphan still holds. A session whose stream has finished — a final
  `result`, nothing pending in the background — but whose CLI does not exit is closed after
  `budget.settleSeconds` (60, `AGENT_SETTLE_SECONDS`) of quiet and judged as finished, not timed out.
- **A task is verified on its own change.** The runner and the brief run verify with
  `--scope working` — what is committed on the branch was verified when it was committed — while
  `/verify`, `/ship` and a pull request still judge the whole branch. Inside a run, the lead's plain
  `npm run verify` is answered by the runner (`AGENT_VERIFY_URL`, a loopback endpoint), which runs
  it itself and reuses that pass after the session when the tree has not changed, so the same gate
  is not run twice; `npm run auto:status` shows each run's verify time.
- **Every session runs on `opus`**, the alias for the newest Opus: the runner and the planner
  through `budget.model` (overridden by `AGENT_MODEL`; `inherit` passes no `--model`), and every
  agent in `.claude/agents/` through its `model:` line.
- **The CLI's output is a contract.** Everything the runner decides — cost, turns, delegations,
  skills, limit stops, account refusals — is parsed from `claude -p --output-format stream-json`
  in `scripts/lib/claude-events.mjs`, and nowhere else. `scripts/tests/claude-stream-contract.test.mjs`
  replays streams captured from real CLI versions (`scripts/tests/fixtures/claude-stream/<version>/`).
  `npm run loop` runs `npm run test:claude-contract` once per new CLI version and builds nothing
  when it fails; after fixing the adapter, `npm run test:claude-contract -- --record` captures the
  new version's fixtures.
- **Guards run in every permission mode.** `.claude/hooks/` refuses reading or writing `.env*` and the
  per-environment `appsettings.*.json`, edits to build output, `dotnet ef database drop`, destructive
  SQL, `git reset --hard` and its equivalents (`checkout -- .`, `restore .`, `stash clear`,
  `branch -D`), `git add -A`, force-pushes, pushes to a protected branch (and, in `team`, a commit
  on the base branch), every merge route and the VPS deploy (`npm run deploy:vps`, through `hooks.deniedCommands`) — for the Bash and PowerShell tools alike, with git's global options (`git -C …`) seen
  through — and reading the secret files through the Read and Grep tools. `npm run test:hooks`
  holds a block case and a neighbouring allow case for each rule; add both when you add a rule.
- **Records.** `.agent-runs/` (git-ignored) is every attempt, with its raw stream beside its log
  (`<run>-attempt-N.stream.jsonl`); each task's commit is its code, its brief leaving `todo/` and its
  build record — `docs/builds/<date-time>-<spec>/<task>.md`, one directory per planning of a spec, stamped with when
  planning started (`docs/builds/adhoc/` for a task queued by hand), written just before the commit;
  `.claude/memory/lessons/` is what runs learned, injected into later briefs; a lesson names
  no task, since the task that taught it means nothing to the unrelated ones that read it.

## Task guides

`.claude/skills/` holds step-by-step guides for the recurring tasks in this codebase. Consult the matching one before writing code so new work follows the same shape as the existing features.

- Cross-cutting: `coding-conventions`
- API: `backend-feature`, `backend-endpoint`, `backend-entity`, `backend-tests`, `permissions`, `settings`, `background-jobs`, `file-storage`, `notifications`
- Web: `rtk-query-api`, `frontend-page`, `frontend-crud`, `ui-component`, `redux-state`, `localization`, `frontend-tests`
- Spanning both: `api-error-handling`, `seo` (public pages, their metadata, the sitemap and robots)
- Committing: `commit` — stages deliberately and commits as the configured git user, with no co-author or attribution trailer
- Process: spec-driven development — `specs/README.md`, the `.claude/commands` (`/orient`, `/feature`, `/fix`, `/auto`, `/queue`, `/spec-split`, `/check`, `/verify`, `/ship`, `/review-diff`, `/trace`, `/pr-msg`), and the agents in `.claude/agents`
