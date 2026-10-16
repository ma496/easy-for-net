---
name: coding-conventions
description: Naming, file layout, and code-style rules for this codebase (C# API and Next.js web). Use before writing or reviewing any C# or TypeScript file so new code matches the surrounding style — namespaces, XML docs, sealed/primary constructors, kebab-case web files, i18n key shapes.
---

# Coding conventions

Read this before adding files to either stack. Every rule below is what the existing code
already does — match it rather than introducing a personal style.

## C# (`src/backend/Source`, `src/backend/Tests`)

**File header order.** The `using` directives come **first**, at the top of the file. Then one
blank line, then the file-scoped namespace declaration, then a blank line, then the XML doc and
the type:

```csharp
using Backend.Features.Tenancy.Core;
using Backend.Features.Tenancy.Core.Entities;

namespace Backend.Features.Tenancy.Endpoints.Editions;

/// <summary>
/// This endpoint that handles <c>POST /editions</c> to create a plan the platform can put tenants on.
/// </summary>
sealed class EditionCreateEndpoint(IEditionService editionService) : Endpoint<EditionCreateRequest, EditionCreateResponse>
```

Usings outside the namespace sit at the same level as the `global using` directives in
`Meta.cs`, so a file's own imports compete with the global ones on equal terms. When a type name
exists in two imported namespaces (`TokenRequest` is both the Identity sign-in request and
`FastEndpoints.Security.TokenRequest`), the reference is ambiguous and needs an alias
(`using TokenRequest = Backend.Features.Identity.Endpoints.Account.TokenRequest;`), never a
`using` moved below the namespace. Write a namespace import in full (`using Backend.Features.X.Core;`),
never relative to the file's own namespace. A file that needs no import opens with the namespace
line. `.editorconfig` sets `csharp_using_directive_placement = outside_namespace`, so the IDE's
"add missing using" puts a new import in the right place.

Two kinds of file have no namespace and are exempt: `Meta.cs` in each project, which is nothing
but `global using` directives, and `Program.cs`, whose top-level statements require its usings at
the top of the file.

**Global usings live in `Meta.cs`** — `FastEndpoints` (+ `.Security`, `.Swagger`),
`FluentValidation`, `Riok.Mapperly.Abstractions`, `Microsoft.EntityFrameworkCore`,
`Microsoft.Extensions.Options`, and the project's `Backend`, `Backend.Base`, `Backend.Base.Dto`,
`Backend.ShareData`, `Backend.Permissions`, `Backend.ErrorHandling`, `Backend.Attributes`,
`Backend.Extensions`, `Backend.Exceptions` and `Backend.Features.Tenancy.Core.FeatureManagement`.
Never re-import those in a file; add genuinely project-wide usings to `Meta.cs` instead.
`Backend.ShareData.Entities.Base` and each feature's own namespaces are *not* global — import them.
The test project has its own `Meta.cs` (adds xUnit, FluentAssertions, Bogus,
FastEndpoints.Testing, `System.Net`, `Backend.Tests.Seeder`).

**Accessibility.** `.editorconfig` sets `dotnet_style_require_accessibility_modifiers = never:error`,
so *omit the default modifier*: endpoints, requests, validators, groups, internal DTOs and helper
classes are declared as plain `sealed class Xyz` / `static class Xyz`, never `internal …`.
Mark a type `public` only when a public signature or generated code needs it (response/DTO types,
entities, services and their interfaces, feature classes, Mapperly mappers). Tests can still see internal types — `Meta.cs` grants
`InternalsVisibleTo("Backend.Tests")`.

**Types are `sealed` by default.** Endpoints, requests, validators, responses, groups. Mapperly
mappers are `public partial class` (the generator needs `partial`; a mapper of static methods is a
`static partial class`). Entities, configurations and
service implementations are plain `public class`.

**Primary constructors for dependencies.** `sealed class UserGetEndpoint(IUserService userService)`,
`public class EditionService(AppDbContext dbContext, …) : IEditionService`. No constructor bodies,
no readonly backing fields.

**Modern C# is expected:** `var` everywhere, collection expressions (`[]`, `[.. items.Select(x => …)]`),
target-typed `new()`, pattern matching, expression-bodied members for one-liners.

**EF Core reads are untracked projections.** `.AsNoTracking()` + `.Select(...)` to the columns the
code uses, `AnyAsync`/`CountAsync` rather than loading rows, and an `OrderBy` before every
`Skip`/`Take`; only an entity being updated is loaded tracked. The `backend-endpoint` skill has the
details.

**XML documentation on every type** — the whole codebase carries `/// <summary>` on classes,
interfaces, and non-obvious public methods. Add it; do not leave new types undocumented.

**Naming:**

| Thing | Pattern | Example |
| --- | --- | --- |
| Endpoint | `<Entity><Action>Endpoint` | `UserCreateEndpoint` |
| Request / Response | `<Entity><Action>Request` / `…Response` | `UserListRequest` |
| Validator | `<Entity><Action>Validator` | `UserDeleteValidator` |
| Mapper | `<Entity><Action>{Request,Response,Dto}Mapper` | `UserCreateRequestMapper` |
| Route group | `<Area>Group` | `UsersGroup`, `EditionsGroup` |
| Row DTO in a list | `<Entity>ListDto` | `UserListDto` |
| Entity config | `<Entity>Configuration` | `NotificationConfiguration` |
| Service | `I<Name>Service` + `<Name>Service` | `IEditionService` / `EditionService` |
| Narrow read contract | `I<Name>Query` + `<Name>Query` | `ITenantMembershipQuery` |
| Shared validation rules | `<Entity>ValidationRules` | `TenantValidationRules` |
| Feature module (DI) | `<Feature>Feature` | `IdentityFeature` |
| Permission provider | `<Feature>PermissionsProvider` | `IdentityPermissionsProvider` |
| Entitlement provider | `<Feature>FeaturesProvider` | `FileManagementFeaturesProvider` |
| Permission constant | `Entity_Action` (`"Entity.Action"` value) | `User_Create = "User.Create"` |
| Feature (entitlement) constant | `Feature_Name` (`"Feature.Name"` value) | `Identity_MaxUserCount` |
| Error code | `ErrorCodes` member (`public static readonly ErrorCode`), `camelCase` value | `UsernameAlreadyExists = new("usernameAlreadyExists")` |

Private static readonly fields are `_camelCase`, private constants `PascalCase`, parameters
`camelCase` (enforced as warnings by `.editorconfig`).

**Formatting.** 4-space indent, CRLF, UTF-8, and `insert_final_newline = false` — do not add a
trailing newline to C# files.

## TypeScript / React (`src/frontend/web`)

**File names are kebab-case**: `user-create-form.tsx`, `use-table-url-state.ts`, `users-api.ts`,
`users-dtos.ts`. Route folders are kebab-case too (`change-permissions`), private folders are
prefixed with `_` (`_components`), and admin screens sit in a route group per backend feature
(`admin/(identity)/users`, `admin/(tenancy)/editions`).

**Exports.** Named exports for components, hooks and APIs (`export const UserTable = () => …`);
`export default` only for Next.js `page.tsx` / `layout.tsx`. Barrel files (`components/ui/index.ts`,
`hooks/index.ts`, `lib/utils/index.ts`, `store/api/index.ts`, `store/api/<feature>/index.ts`)
re-export everything — add your new symbol to the matching barrel and import from the barrel
(`import { Button } from '@/components/ui'`), not from the deep path.

**Style.** 2-space indent, single quotes, no semicolons (`.prettierrc`: `semi: false`,
`singleQuote: true`, `printWidth: 200`), arrow-function components, `'use client'`
as the very first line of any client component. Props interfaces are named `<Component>Props` and
declared right above the component. Exported components, hooks and DTO interfaces carry a JSDoc
`/** … */` one-liner.

**Imports use the `@/` alias** (`@/store/api/identity`, `@/components/ui/form`, `@/lib/utils`,
`@/i18n`, `@/hooks`, `@/allow`, `@/feature-names`).

**No hard-coded user-visible strings.** Everything goes through `t('…') || 'English'` from `@/i18n`
(client) or `(await getServerTranslation(lang, '…')) || 'English'` (server component), with the key added to the API's
`Features/Localization/Core/Resources/en.json` — and to every other `<code>.json` there when the
project is multi-language. Import translators from `@/i18n` itself, never a subpath. Key namespaces
already in use:

- `page.<area>.*` — page titles and page-specific copy (`page.users.create.title`)
- `form.label.*`, `form.placeholder.*` — form field text
- `validation.*` — Yup messages (`validation.required`, `validation.minLength`, `validation.tenantIdentifier`)
- `table.columns.*`, `table.actions`, `table.export.*`, `table.filter.*` — data-table chrome
- `navigation.*` / `search.*` — sidebar entries and global search entries
- `error.server.*` — messages keyed off backend error codes
- `common.*` — shared verbs (`common.submit`, `common.cancel`)

Interpolation uses `${name}` inside the JSON value and `t('validation.minLength', { min: 3 }) || 'Field must be at least 3 characters'`.
Every call carries its English default after `||` — the key's exact `en.json` value — since a missing
key translates to `''` (see the `localization` skill).

**Types mirror the API.** A DTO interface in `store/api/**/…-dtos.ts` has the same name as the
C# class it mirrors, with camelCase members, and extends `BaseDto<string>`, `GenericAuditableDto<string>`,
`ListRequestDto<string>`, `ListDto<T>` or `RequestBase` from `@/store/api`. The C# DTO marker
interfaces map to their counterparts without the `I`: `ISystemCreatedDto` → `SystemCreatedDto`,
`IMayHaveTenantDto` → `MayHaveTenantDto`, `IHaveTenantDto` → `HaveTenantDto`. `Guid` maps to
`string` (`Guid?` to `string | null`), `DateTime` maps to `string`.
