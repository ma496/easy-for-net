---
name: new-project
description: Scaffold a new application from this template with the dotnet efn CLI (efn cp -n <name>), and do the post-create setup — database password, initial migration, running the API and the web app. Use when asked to create/start a new project, or when a freshly generated project does not build or run yet.
---

# Creating a new project with `dotnet efn`

## Install / update the tool

```sh
dotnet tool install -g EasyForNetTool      # first time
dotnet tool update -g EasyForNetTool       # later
dotnet efn --version
dotnet efn cp --help
```

Prerequisites: the .NET SDK pinned in `global.json` (10.0.x), **git on PATH** (the tool clones the
template), Node >= 24, and a reachable PostgreSQL.

## Generate

```sh
dotnet efn cp -n my-shop                   # createproject, short form
dotnet efn cp --name my-shop --output projects --multilanguage true
```

| Option | Meaning |
| --- | --- |
| `-n`, `--name` (required) | Letters, `-` and `_` only; must start and end with a letter. Becomes `MyShop` for namespaces/projects and `my-shop` for the folder and the npm package name. |
| `-o`, `--output` | Directory to create the project in, relative to the current directory. Defaults to the current directory. |
| `-m`, `--multilanguage` | `false` (default) ships English only; `true` keeps all eight locales. |

The tool clones `https://github.com/ma496/EasyForNet.git` into
`%LOCALAPPDATA%/EasyForNet/Templates/<tool version>` and checks out the tag `v<tool version>`, so
the template always matches the installed tool. The target directory must not already exist.

What you get: `src/backend/{Source,Tests}`, `src/frontend/web`, `.config`, `.vscode`,
`.editorconfig`, `.gitignore`, `global.json`, a `CLAUDE.md`, the `.claude/skills` guides, and a
`<Name>.slnx` solution with both backend projects added. Namespaces, project file names,
`InternalsVisibleTo`, the npm package name and the app's display name are rewritten to the project
name.

**Migrations are deliberately not copied** — a new project owns its own migration history.

## Post-create setup

1. **Database password.** Connection strings are written with a literal `{password}` placeholder in
   `src/backend/Source/appsettings.json`, `appsettings.Development.json` and
   `appsettings.Testing.json`. Replace it in all three. The databases are `<Name>` and `<Name>Test`.
2. **Initial migration.**

   ```sh
   dotnet tool restore
   dotnet ef migrations add Initial --project src/backend/Source/<Name>.csproj
   ```

   Development and Testing apply migrations on startup, so `dotnet ef database update` is optional.
3. **Run the API.**

   ```sh
   dotnet run --project src/backend/Source/<Name>.csproj
   ```

   Startup seeds the permission catalog, the `Admin` role and the `admin` user
   (`admin` / `Admin#123`). Swagger is served in Development; Hangfire's dashboard is at `/hangfire`;
   health at `/health`.
4. **Run the web app.**

   ```sh
   cd src/frontend/web
   npm install
   npm run dev
   ```

   `.env.development` is seeded from `.env.example` with
   `NEXT_PUBLIC_API_URL=http://localhost:5000/api` — point it at whatever port the API actually
   uses.
5. **Tests.** `dotnet test src/backend/Tests/<Name>.Tests.csproj` needs PostgreSQL reachable with
   the `appsettings.Testing.json` connection string; the suite creates and drops its own database.
6. **Before deploying anywhere non-development**, supply `Auth:Jwt:Key` (>= 32 chars), `Auth:Jwt:Issuer`
   and `Auth:Jwt:Audience` through secure configuration — startup refuses to boot with the
   placeholder key outside Development/Testing. `Web:Domains` must list the real front-end origins
   (they drive CORS) and `Database:ApplyMigrationsOnStartup` defaults to false there.

## Then

The generated project's `.claude/skills` folder carries the code guides from the template: feature
and endpoint layout, entities and migrations, permissions, tests, RTK Query, pages and CRUD
screens. (This scaffolding guide and `template-maintenance` are left out — they describe working on
the template repository, not on the generated application.) Follow those when adding code so the
project keeps the shape the template assumes.
