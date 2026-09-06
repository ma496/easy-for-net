# Frameworks

-   **Backend:**
    -   **Source Project:** .NET 10 using FastEndpoints and Entity Framework Core (PostgreSQL).
    -   **Test Project:** xUnit v3 with FastEndpoints.Testing, FluentAssertions, and Bogus.
-   **Frontend:**
    -   **Web Project:** Next.js (App Router) with React 19, Redux Toolkit, and Tailwind CSS.

# C# Coding Conventions

*   **Naming Conventions:**
    *   Use `PascalCase` for class names, method names, and properties.
    *   Use `camelCase` for local variables and method parameters.
    *   Prefix private fields with an underscore (`_`) (e.g., `_privateField`).
    *   Prefix interfaces with `I` (e.g., `IService`).
*   **Async Methods:**
    *   Methods that are asynchronous should have the `Async` suffix.
*   **File Organization:**
    *   A class and its corresponding interface should be in the same file, with the interface defined first.
    *   The filename should match the name of the public class it contains.
    *   Files should not have an empty line at the beginning.
    *   Use file-scoped namespaces at the start of the file. Follow the namespace declaration with a single empty line, then list `using` directives, followed by the rest of the code.
    *   Remove unused using namespaces.
*   **Formatting:**
    *   Tab space should be 4 spaces.
*   **Namespace Management:**
    *   Before writing C# code, identify and gather all required namespaces. If these namespaces are already included in the project Meta.cs file, do not add them again to avoid redundancy. Ensure the code remains clean and efficient by referencing only necessary namespaces that are not already present in project Meta.cs.
*   **General Style:**
    *   Use primary constructors where possible to simplify class definitions.
    *   Prefer `class` types for Data Transfer Objects (DTOs).
    *   Use collection expressions for initializing collections (e.g., `List<string> names = [];`).
    *   Use `var` for local variable declarations when the right-hand side of the assignment makes the type obvious.
    *   Use expression-bodied members for simple, single-line methods and properties.
    *   Utilize LINQ for querying and manipulating collections where it enhances readability. Avoid overly complex LINQ chains that are hard to debug.
    *   The Entity Framework Core `DbContext` object should be named `dbContext`.

# TypeScript Coding Conventions

*   **Naming Conventions:**
    *   Use `PascalCase` for type names, interfaces, and React components (e.g., `UserProfile`, `IUser`).
    *   Use `camelCase` for variables, functions, and properties (e.g., `userName`, `getUserProfile`).
*   **File Naming:**
    *   Use `kebab-case` for all `.ts` and `.tsx` files (e.g., `user-profile.tsx`).
*   **Null and Undefined:**
    *   `strictNullChecks` is enabled. Avoid using `null` unless it is meaningful for a specific API. Prefer `undefined` for optional properties and return values.
*   **General Style:**
    *   Prefer `interface` over `type` for all object-like structures. Use `type` only for union types, intersection types (if not possible via interface extends), or utility types.
    *   Use `const` by default and `let` only when a variable needs to be reassigned.
    *   Use arrow functions for component and callback definitions.
    *   Leverage modern TypeScript features like optional chaining (`?.`) and nullish coalescing (`??`).
    *   Organize imports at the top of the file in the following order: external libraries, project-absolute paths, relative paths.
*   **Formatting:**
    *   Tab space should be 2 spaces (applies to both `.ts` and `.tsx` files).

# JSON Coding Conventions

*   **Formatting:**
    *   Tab space should be 2 spaces.

# CSS Coding Conventions

*   **Formatting:**
    *   Tab space should be 2 spaces.

# Build and Test Rules

## Backend

**Location:** `src/backend`
**Solution File:** `EasyForNet.slnx`

### Build
Run from the root of the repository or `src/backend`:
```powershell
dotnet build EasyForNet.slnx
```

### Test
Run from the root of the repository or `src/backend`:
```powershell
dotnet test EasyForNet.slnx
```

## Frontend

**Location:** `src/frontend/web`

### Catch Typescript Errors
Run from `src/frontend/web`:
```powershell
cd src/frontend/web
npx tsc --noEmit
```

### Lint
Run from `src/frontend/web`:
```powershell
cd src/frontend/web
npm run lint
```

### Build
Run from `src/frontend/web`:
```powershell
cd src/frontend/web
npm run build
```
