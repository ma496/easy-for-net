---
name: coding-conventions
description: Naming, file layout, and code-style rules for this codebase (C# API and Next.js web). Use before writing or reviewing any C# or TypeScript file so new code matches the surrounding style — namespaces, XML docs, sealed/primary constructors, kebab-case web files, i18n key shapes.
---

# Coding conventions

Read this before adding files to either stack. Every rule below is what the existing code
already does — match it rather than introducing a personal style.

## C# (`src/backend/Source`, `src/backend/Tests`)

**File header order.** File-scoped namespace first, `using` directives *after* it, then the
XML doc, then the type:

```csharp
namespace Backend.Features.Identity.Endpoints.Users;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;

/// <summary>
/// This endpoint that handles <c>POST /users</c> to create a new user with the supplied roles.
/// </summary>
sealed class UserCreateEndpoint(IUserService userService, AppDbContext dbContext) : Endpoint<UserCreateRequest, UserCreateResponse>
```

**Global usings live in `Meta.cs`** — FastEndpoints, FluentValidation, Mapperly, EF Core,
`Backend.Data`, `Backend.Base`, `Backend.Base.Dto`, `Backend.Permissions`,
`Backend.ErrorHandling`, `Backend.Attributes`, `Backend.Extensions`. Never re-import those in a
file; add genuinely project-wide usings to `Meta.cs` instead. The test project has its own
`Meta.cs` (adds xUnit, FluentAssertions, Bogus, FastEndpoints.Testing).

**Accessibility.** `.editorconfig` sets `dotnet_style_require_accessibility_modifiers = never:error`,
so *omit the default modifier*: endpoints, requests, validators and internal DTOs are declared as
plain `sealed class Xyz`, not `internal sealed class Xyz`. Mark a type `public` only when it must
cross an assembly boundary (response/DTO types consumed by generated clients, entities, services,
feature classes). Tests can still see internal types — `Meta.cs` grants
`InternalsVisibleTo("Backend.Tests")`.

**Types are `sealed` by default.** Endpoints, requests, validators, responses, groups. Mapperly
mappers are `public partial class` (the generator needs `partial`).

**Primary constructors for dependencies.** `sealed class UserGetEndpoint(IUserService userService)`.
No constructor bodies, no readonly backing fields.

**Modern C# is expected:** `var` everywhere, collection expressions (`[]`, `[.. items.Select(x => …)]`),
target-typed `new()`, pattern matching, expression-bodied members for one-liners.

**XML documentation on every type** — the whole codebase carries `/// <summary>` on classes,
interfaces, and non-obvious public methods. Add it; do not leave new types undocumented.

**Naming:**

| Thing | Pattern | Example |
| --- | --- | --- |
| Endpoint | `<Entity><Action>Endpoint` | `UserCreateEndpoint` |
| Request / Response | `<Entity><Action>Request` / `…Response` | `UserListRequest` |
| Validator | `<Entity><Action>Validator` | `UserDeleteValidator` |
| Mapper | `<Entity><Action>{Request,Response,Dto}Mapper` | `UserCreateRequestMapper` |
| Route group | `<Area>Group` | `UsersGroup`, `FileGroup` |
| Row DTO in a list | `<Entity>ListDto` | `UserListDto` |
| Entity config | `<Entity>Configuration` | `NotificationConfiguration` |
| Service | `I<Name>Service` + `<Name>Service` | `IUserService` / `UserService` |
| Feature module | `<Feature>Feature` | `IdentityFeature` |
| Permission provider | `<Feature>PermissionsProvider` | `IdentityPermissionsProvider` |
| Permission constant | `Entity_Action` (`"Entity.Action"` value) | `User_Create = "User.Create"` |
| Error code | `camelCase` string constant | `usernameAlreadyExists` |

Private static readonly fields are `_camelCase`, private constants `PascalCase`, parameters
`camelCase` (enforced as warnings by `.editorconfig`).

**Formatting.** 4-space indent, CRLF, UTF-8, and `insert_final_newline = false` — do not add a
trailing newline to C# files.

## TypeScript / React (`src/frontend/web`)

**File names are kebab-case**: `user-create-form.tsx`, `use-table-url-state.ts`, `users-api.ts`,
`users-dtos.ts`. Route folders are kebab-case too (`change-permissions`), private folders are
prefixed with `_` (`_components`).

**Exports.** Named exports for components, hooks and APIs (`export const UserTable = () => …`);
`export default` only for Next.js `page.tsx` / `layout.tsx`. Barrel files (`components/ui/index.ts`,
`hooks/index.ts`, `lib/utils/index.ts`, `store/api/index.ts`, `store/api/<feature>/index.ts`)
re-export everything — add your new symbol to the matching barrel and import from the barrel
(`import { Button } from '@/components/ui'`), not from the deep path.

**Style.** 2-space indent, single quotes, no semicolons, arrow-function components, `'use client'`
as the very first line of any client component. Props interfaces are named `<Component>Props` and
declared right above the component. Exported components, hooks and DTO interfaces carry a JSDoc
`/** … */` one-liner.

**Imports use the `@/` alias** (`@/store/api/identity`, `@/components/ui/form`, `@/lib/utils`,
`@/i18n`, `@/hooks`, `@/allow`).

**No hard-coded user-visible strings.** Everything goes through `t('…')` from `@/i18n`
(client) or `getServerTranslation(lang, '…')` (server component), with the key added to
`public/locales/en.json` — and to every other `public/locales/<code>.json` when the project is
multi-language. Key namespaces already in use:

- `page.<area>.*` — page titles and page-specific copy (`page.users.create.title`)
- `form.label.*`, `form.placeholder.*` — form field text
- `validation.*` — Yup messages (`validation.required`, `validation.minLength`)
- `table.columns.*`, `table.actions`, `table.export.*`, `table.filter.*` — data-table chrome
- `navigation.*` / `search.*` — sidebar entries and global search entries
- `error.server.*` — messages keyed off backend error codes
- `common.*` — shared verbs (`common.submit`, `common.cancel`)

Interpolation uses `${name}` inside the JSON value and `t('validation.minLength', { min: 3 })`.

**Types mirror the API.** A DTO interface in `store/api/**/…-dtos.ts` has the same name as the
C# class it mirrors, with camelCase members, and extends `BaseDto<string>`, `GenericAuditableDto<string>`,
`ListRequestDto<string>`, `ListDto<T>` or `RequestBase` from `@/store/api`. `Guid` maps to `string`,
`DateTime` maps to `string`.
