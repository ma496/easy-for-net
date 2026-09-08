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
dotnet run --project src/backend/Source
```

Backend tests — **require a running PostgreSQL** matching `appsettings.Testing.json`; the Testing environment migrates and seeds the database on startup:

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

Default credentials seeded on first run: `admin` / `Admin#123`.

## Backend architecture

**Vertical slices under `Features/`.** Each feature is `Features/<Feature>/{Core,Endpoints}`:

- `Core` holds entities (`Core/Entities`, EF configuration in `Core/Entities/Configuration`), services, settings, and the feature's `IPermissionDefinitionProvider`.
- `Endpoints/<Area>` holds one file per endpoint plus an `<Area>Group.cs` that owns the route prefix.
- `<Feature>Feature.cs` implements `IFeature.AddServices`. `Helper.AddFeatures` reflects over the assembly at startup and calls it — features are never registered by hand in `Program.cs`.

**One endpoint = one file.** `Features/Identity/Endpoints/Users/UserCreateEndpoint.cs` is the canonical shape: the `sealed class ...Endpoint : Endpoint<TReq, TRes>`, its `...Request`, a FluentValidation `...Validator : Validator<TReq>`, the `...Response`/DTOs, and the Riok.Mapperly `[Mapper] partial class ...Mapper` all live together. Types are internal where possible; `Meta.cs` grants `InternalsVisibleTo` to the test project so tests can reference them.

**Global usings live in `Meta.cs`** (FastEndpoints, FluentValidation, Mapperly, EF Core, the `Data`, `Base.Dto` and `Permissions` namespaces, …). Do not re-add those per file; add new project-wide ones there.

**Feature isolation is enforced by tests.** `Tests/Architect/FeatureDependencyTests` fails if a type under `<RootNamespace>.Features.X` depends on a type under `<RootNamespace>.Features.Y` unless that type is marked `[AllowOutside]`. `[NoDirectUse]` (with `[BypassNoDirectUse]` as the escape hatch) enforces consuming a class through its interface. `Tests/Architect/Features/Feature{A,B}` are fixtures that exercise the rules themselves — not real features.

**Permissions** are string constants in `Permissions/Allow.cs`, declared as a hierarchy by each feature's `IPermissionDefinitionProvider`, enforced on endpoints via `Permissions(Allow.X)`, and reconciled into the database by `Data/DataSeeder` on every startup (adds/renames/deletes rows and strips deleted permissions from roles). Adding a permission means: constant in `Allow.cs` → definition in the provider → mirror the constant in `src/frontend/web/allow.ts`.

**Data access.** `AppDbContext` applies entity configurations from the assembly, installs a global soft-delete query filter for `ISoftDelete`, and fills audit/normalized properties on save. List endpoints take a `ListRequestDto<TId>` and call `IQueryableExtension.Process(request)` for sorting/paging; sortable fields must be whitelisted in the request validator.

**Auth.** A `Jwt_Or_Cookie` policy scheme picks JWT bearer when an `Authorization: Bearer` header is present, cookies otherwise. Claims include a `SessionVersion` derived from the password hash (`Helper.CreateSessionVersion`); `SessionValidationMiddleware` rejects tokens whose version no longer matches, so a password change invalidates existing sessions. Refresh tokens and forgot-password tokens are cleaned by Hangfire recurring jobs registered at the end of `Program.cs`.

**Errors.** Endpoints call `ThrowError(message, ErrorCodes.X)` with codes from `ErrorHandling/ErrorCodes.cs`; FastEndpoints serializes ProblemDetails with the error code included, and `ExceptionProcessor` / `ToLargePayloadProcessor` / `UnsupportedMediaTypeResponseProcessor` are wired globally in `Program.cs`.

**Startup guards** worth knowing before changing configuration: the JWT key placeholder is rejected outside Development/Testing, `Database:ApplyMigrationsOnStartup` defaults to true only in Development/Testing, and the `Payload`/`Web`/`Auth` options are `ValidateOnStart`.

## Backend tests

xUnit v3 + `FastEndpoints.Testing`. `App : AppFixture<Program>` runs the host with environment `Testing`; `AppTestsBase` (in the `SharedContext` collection) exposes `App.Client`, `DbContext`, `SetAuthTokenAsync()` (defaults to `admin` / `Admin#123`), and `CreateAdminUserAsync`. Tests call endpoints type-safely — `App.Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, UserCreateResponse>(request)` — assert with FluentAssertions, and build payloads with Bogus `Faker<T>`. Shared fixtures live in `Tests/Seeder` (`TestRoles`, `TestUsers`, `TestsDataSeeder`); reuse them instead of creating ad-hoc roles. Collections run in parallel, so a test must not depend on global database state it did not create.

## Frontend architecture

Next.js App Router under `app/[lang]/` — **every route is locale-prefixed**. Route groups: `(public)`, `(auth)` (signin/signup/profile/password flows), and `admin/`. Page components are server components that resolve `params`, fetch a title via `getServerTranslation(lang, key)`, and render an interactive client component from a sibling `_components/` folder inside `AdminPageContent`.

`proxy.ts` (Next 16's renamed middleware) does two things: locale negotiation (rewrite for the default locale, redirect otherwise; locales in `i18n/config.ts`, messages in `public/locales/<locale>.json`) and auth gating based on `auth-urls.ts` plus the auth cookie.

**Data layer is RTK Query.** `store/api/_app-api.ts` defines the single `appApi` with a `baseQueryWithReauth` that serializes refresh attempts through an `async-mutex` and signs the user out (redirecting to `/signin?redirect=…`) when refresh fails. Feature APIs live in `store/api/<feature>/<area>/{<area>-api.ts,<area>-dtos.ts}` and attach via `appApi.enhanceEndpoints({ addTagTypes: [...] }).injectEndpoints(...)` — never call `createApi` again. DTO types mirror the backend request/response classes by name.

Client permission checks use the `Allow` map in `allow.ts`, kept in sync with the backend constants. Shared pieces: `components/{custom,ui,layouts,notifications}`, `hooks/` (`use-table-url-state`, `use-localized-router`, `use-debounce`, `use-notification-hub` — polling, not a socket), `lib/utils/` (API error helpers, auth helpers). Navigation and global search entries are declared in `nav-items.ts` and `searchable-items.ts`.

Adding a language means adding it to `i18n/config.ts` and adding `public/locales/<code>.json`. If the project was created without multi-language support, only English is present and `i18n/config.ts`, `i18n/server.ts` and `store/slices/themeConfigSlice.tsx` list `en` alone.

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

The stages are separate runs because a workflow cannot ask a question while it runs — every point where a human decision belongs is a stage boundary. `AC-nnn` and `T-nnn` ids are permanent and tie criteria to tasks to evidence. See the `spec-driven` skill for the loop, the document templates and the completeness checklist.

One full loop is roughly 200 subagent calls, so it is for real features, not one-line fixes. `specs/` is created on first use and is committed to the repository.

## Task guides

`.claude/skills/` holds step-by-step guides for the recurring tasks in this codebase. Consult the matching one before writing code so new work follows the same shape as the existing features.

- Process: `spec-driven`
- Cross-cutting: `coding-conventions`
- API: `backend-feature`, `backend-endpoint`, `backend-entity`, `backend-tests`, `permissions`, `background-jobs`, `file-storage`, `notifications`
- Web: `rtk-query-api`, `frontend-page`, `frontend-crud`, `ui-component`, `redux-state`, `localization`, `frontend-tests`
- Spanning both: `api-error-handling`
