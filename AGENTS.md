# AGENTS.md - Coding Guidelines for EasyForNet

This document provides essential information for AI coding agents working on the EasyForNet full-stack template project.

## Project Overview

- **Backend:** .NET 10 + FastEndpoints + EF Core (PostgreSQL) + xUnit v3 tests
- **Frontend:** Next.js 16 + React 19 + Redux Toolkit + Tailwind CSS v4
- **Solution:** `EasyForNet.slnx` at repository root

## Build, Test & Lint Commands

### Backend (.NET)
```bash
# Build entire solution
dotnet build EasyForNet.slnx

# Build backend only
dotnet build src/backend/Source/Backend.csproj

# Run all backend tests
dotnet test src/backend/Tests/Backend.Tests.csproj

# Run specific test class
dotnet test src/backend/Tests/Backend.Tests.csproj --filter "FullyQualifiedName~UserGetTests"

# Run specific test method
dotnet test src/backend/Tests/Backend.Tests.csproj --filter "FullyQualifiedName~UserGetTests.Get_User"

# Run tool tests
dotnet test tool/EasyForNetTool.Tests/EasyForNetTool.Tests.csproj

# Add EF migration (run from src/backend/Source)
dotnet ef migrations add MigrationName

# Run backend
dotnet run --project src/backend/Source/Backend.csproj
```

### Frontend (Next.js)
```bash
cd src/frontend/web

# Install dependencies
npm install

# Start dev server
npm run dev

# Build for production
npm run build

# Run ESLint
npm run lint

# No test command configured yet
```

## Code Style Guidelines

### C# Conventions

**Naming:**
- `PascalCase`: classes, methods, properties, public members
- `camelCase`: local variables, parameters
- `_camelCase`: private fields with underscore prefix
- `I` prefix: interfaces (e.g., `IService`)
- `Async` suffix: async methods

**Formatting:**
- 4-space indentation for `.cs` files
- 2-space indentation for `.json`, `.js`, `.jsx`, `.ts`, `.tsx`
- File-scoped namespaces at start of file
- No empty line at beginning of files
- Use `var` when type is obvious
- Primary constructors preferred
- Collection expressions: `List<string> items = [];`

**File Organization:**
- Interface and class in same file (interface first)
- Filename matches public class name
- Remove unused usings (check Meta.cs for project-wide usings)
- Order: namespace declaration → empty line → using directives → code

**Error Handling:**
- Always provide error message AND error code with `ThrowError`
- Define error codes in `src/backend/Source/ErrorHandling/ErrorCodes.cs`
- Naming: `ErrorDescription` format (e.g., `ProductNotFound`)

### TypeScript/TSX Conventions

**Naming:**
- `PascalCase`: types, interfaces, React components
- `camelCase`: variables, functions, properties
- `kebab-case`: file names (e.g., `user-profile.tsx`)

**Formatting:**
- 2-space indentation
- No semicolons (Prettier config)
- Single quotes
- 200 character print width
- Prefer `interface` over `type` for objects

**Imports:**
- Order: external libraries → project-absolute (`@/`) → relative
- Use `@/` alias for project imports

**React Patterns:**
- Arrow functions for components
- Server components by default (Next.js App Router)
- `'use client'` directive for client components with hooks
- Use `const` by default, `let` only for reassignment

## Architecture Patterns

### Backend Endpoints (FastEndpoints)

**Single File Per Endpoint:** Each endpoint file contains in order:
1. `Endpoint` class (sealed, primary constructor)
2. `Request` class (sealed)
3. `Validator` class (sealed, inherits `Validator<T>`)
4. `Response` class (sealed)
5. `Mapper` classes (partial, using Mapperly)

**Endpoint Structure:**
```csharp
sealed class UserGetEndpoint(IUserService svc) : Endpoint<UserGetRequest, UserGetResponse>
{
    public override void Configure()
    {
        Get("{id}");
        Group<UsersGroup>();
        Permissions(Allow.User_View);
    }

    public override async Task HandleAsync(UserGetRequest req, CancellationToken ct)
    {
        var entity = await svc.Users().AsNoTracking().FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (entity == null) { await Send.NotFoundAsync(ct); return; }
        await Send.ResponseAsync(new UserGetResponseMapper().Map(entity), cancellation: ct);
    }
}
```

**Mapperly Mapping Rules:**
- Request→Entity: `[Mapper(RequiredMappingStrategy.Source)]`
- Entity→Response: `[Mapper(RequiredMappingStrategy.Target)]`
- Use `[MapProperty("Source", "Target")]` for different names
- Use `[MapProperty(..., Use = nameof(CustomMethod))]` for type conversion
- Use `[MapperIgnoreSource]` / `[MapperIgnoreTarget]` for sensitive/manual fields

**Service Classes:**
- Only create services for: shared functionality, cross-feature usage
- Interface and class in same file (interface first)
- Mark class with `[NoDirectUse]` attribute
- For cross-feature: use `[AllowOutside]` on interface
- Register in Feature class implementing `IFeature` with `[BypassNoDirectUse]`

**Group Classes:**
- Create for related endpoints (e.g., `UsersGroup`)
- Configure base route: `Configure("users", ep => { })`

**Database:**
- Use `dbContext.Entities` properties, not `dbContext.Set<T>()`
- Name dbContext variable as `dbContext`
- Normalize strings: `.Trim().ToLowerInvariant()`
- Check unique constraints before create/update

### Frontend Pages (Next.js)

**Directory Structure:**
- Admin pages: `src/frontend/web/app/[lang]/admin/`
- Public pages: `src/frontend/web/app/[lang](public)/`
- Route groups: `src/frontend/web/app/[lang]/admin/(identity)/users/`
- Page components: `_components/` subdirectory

**Page Rules:**
- `page.tsx` must be Server Component with metadata export
- Client functionality in separate component with `'use client'`
- Use Formik for forms with yup validation
- Use pre-built form components from `components/ui/form/`
- Use RTK Query for API calls (errors handled globally)
- Use `isAllowed()` with `Allow` constants for permissions

## Testing Guidelines

### Backend Tests (xUnit v3)

**Test Structure:**
```csharp
public class UserGetTests(App app) : AppTestsBase(app)
{
    [Fact]
    public async Task Get_User()
    {
        await SetAuthTokenAsync();
        // Test implementation using App.Client
    }
}
```

**Key Points:**
- Inherit from `AppTestsBase`
- Use `[Collection("SharedContext")]` attribute
- Use `SetAuthTokenAsync()` for authenticated requests
- Use Bogus faker for test data generation
- Use FluentAssertions: `result.Should().Be(expected)`
- FastEndpoints testing handles HTTP client lifecycle

**Test Tools:**
- FastEndpoints.Testing for integration tests
- FluentAssertions for assertions
- Bogus for fake data
- xUnit v3 (not v2)

## Important Files & Locations

**Backend:**
- Entry: `src/backend/Source/Program.cs`
- DbContext: `src/backend/Source/Data/AppDbContext.cs`
- Error Codes: `src/backend/Source/ErrorHandling/ErrorCodes.cs`
- Permissions: `src/backend/Source/Permissions/Allow.cs`
- Meta (global usings): `src/backend/Source/Meta.cs`

**Frontend:**
- Permissions: `src/frontend/web/allow.ts`
- Store: `src/frontend/web/store/`
- Components: `src/frontend/web/components/`

**Config:**
- Prettier: `src/frontend/web/.prettierrc`
- ESLint: `src/frontend/web/eslint.config.mjs`
- TypeScript: `src/frontend/web/tsconfig.json`
- EditorConfig: `.editorconfig` (4-space C#, 2-space web)

## Default Credentials

- Username: `admin`
- Password: `Admin#123`
