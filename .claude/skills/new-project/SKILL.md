---
name: new-project
description: Scaffold a new application from this template with the dotnet efn CLI (efn cp -n <name>), and do the post-create setup — database password, git repository, initial migration, running the API and the web app, and signing in with the seeded accounts. Use when asked to create/start a new project, or when a freshly generated project does not build or run yet.
---

# Creating a new project with `dotnet efn`

## Install / update the tool

```sh
dotnet tool install -g EasyForNetTool      # first time
dotnet tool update -g EasyForNetTool       # later
dotnet efn --version                       # or -v
dotnet efn cp --help
```

Prerequisites: the .NET SDK pinned in `global.json` (10.0.x), **git on PATH** (the tool clones the
template), Node >= 24, a reachable PostgreSQL on `localhost:5432` (user `postgres`), and a Redis on
`localhost:6379` (`docker compose up -d` in the project root starts both, see step 4).

## Generate

```sh
dotnet efn cp -n my-shop                   # createproject, short form
dotnet efn createproject --name my-shop --output projects
```

| Option | Meaning |
| --- | --- |
| `-n`, `--name` (required) | Letters, `-` and `_` only, at least two characters, starting and ending with a letter. Parts split on `-`/`_` are joined in PascalCase (`my-shop` → `MyShop`) for namespaces, project files and the solution; the kebab-case form (`my-shop`) names the folder and the npm packages. |
| `-o`, `--output` | Parent directory (relative to the current directory, or absolute). The project is created in `<output>/<kebab-name>`, which must not exist yet. Defaults to the current directory. |
| `-m`, `--multilanguage` | Meant to keep all eight locales when `true`. **Currently unusable:** passing it with any value crashes with `Multilanguage not found in ...CreateProjectArgument`. Omit it; the project then ships English only. |

The tool clones `https://github.com/ma496/EasyForNet.git` into
`%LOCALAPPDATA%/EasyForNet/Templates/<tool version>` and checks out the tag `v<tool version>`, so the
template always matches the installed tool. An existing cache directory is reused without fetching;
delete it to force a fresh clone. A failed clone or checkout removes the cache it created.

What you get:

- `src/backend/{Source,Tests}` (no `Migrations`), `src/frontend/web`, and a `<Name>.slnx` with both
  backend projects added.
- `appsettings.Development.json` / `appsettings.Testing.json` copied from `appsettings.json`, and
  `src/frontend/web/.env.development` copied from `.env.example`.
- Root files `.editorconfig`, `.gitignore`, `.gitattributes`, `global.json`, `package.json` (the gate
  and task-loop scripts) and `agentic.config.json`, plus `.config` (pinned `dotnet-ef`) and `.vscode`.
- `CLAUDE.md` and the agentic layer: `.claude` (agents, commands, hooks, `settings.json`, status
  line, skills, `memory/README.md`), `scripts/` (the loop's engine), and an empty task-loop skeleton —
  `specs/{README,TEMPLATE}.md`, `docs/AGENTIC_WORKFLOW.md`, `docs/{builds,capabilities}/README.md`,
  the `.agent-queue/{todo,doing,done,failed}` lanes, `.agent-queue/planned.json` (`{}`) and an empty
  `.claude/memory/lessons`.

Namespaces, project file names, `InternalsVisibleTo`, the `ReflectionCache.AddFrom…` call, the npm
package names (web and root), `agentic.config.json`'s `project.name`, the app's display name and the
`.claude` markdown are rewritten to the project name. The generator does **not** run `git init`,
`npm install` or `dotnet ef`.

## Post-create setup

Run these from the new project's root.

1. **Database password.** Connection strings (`ConnectionStrings:DefaultConnection` and
   `Hangfire:Storage:ConnectionString`) carry a literal `{password}` placeholder in
   `src/backend/Source/appsettings.json`, `appsettings.Development.json` and
   `appsettings.Testing.json`. The databases are `<Name>` and `<Name>Test`. Ask the developer to
   replace it themselves: the hooks refuse Claude reading or editing the per-environment
   `appsettings.*.json` and `.env*` files. Development and Testing already have a generated
   `Auth:Jwt:Key`.
2. **Git repository.** The task loop reads diffs and commits, and the runner refuses a dirty tree:

   ```sh
   git init
   git add .
   git commit -m "Initial project"
   ```

3. **Initial migration.**

   ```sh
   dotnet tool restore
   dotnet ef migrations add Initial --project src/backend/Source
   ```

   Development and Testing apply migrations on startup, so `dotnet ef database update --project
   src/backend/Source` is optional there. Commit the migration.
4. **Redis.** Outside the Testing environment the API needs Redis (`ConnectionStrings:Redis`,
   default `localhost:6379`) and prefixes every key with `Redis:InstanceName`, which the generator set
   to `<Name>:` (`<Name>Test:` in Testing) so several apps can share one Redis server. The project's
   `docker-compose.yml` runs PostgreSQL (user and password `postgres`, unless `DEV_POSTGRES_PASSWORD`
   says otherwise — match it in the connection strings) and Redis with the password `redis` (or
   `DEV_REDIS_PASSWORD`, matched in `ConnectionStrings:Redis` as `localhost:6379,password=<password>`) for development:

   ```sh
   docker compose up -d
   ```

   The ports are bound to loopback and the passwords are development defaults; a shared or deployed
   Redis needs a strong password or ACL (and TLS) in `ConnectionStrings:Redis`, supplied through
   secure configuration. The backend tests (`dotnet test`) do not need Redis.
5. **Run the API.**

   ```sh
   dotnet run --project src/backend/Source
   ```

   It listens on `http://localhost:5000` (routes under `/api`). Swagger is served in Development,
   health at `/health`, and the Hangfire dashboard at `/hangfire` for a signed-in platform account
   only. Every start — in every environment — reconciles the permission catalogue and seeds, when
   absent:
   - the **Default** tenant (identifier `default`);
   - `admin` / `Admin#123` — the platform account: no tenant membership, signs in to platform scope
     (tenants, editions, platform roles, Hangfire);
   - `tenantadmin` / `Admin#123` — administrator of the Default tenant, with sample notifications.
6. **Run the web app.**

   ```sh
   cd src/frontend/web
   npm install
   npm run dev
   ```

   It serves `http://localhost:3000` (the origin `Web:Domains` allows for CORS). `.env.development`
   points `NEXT_PUBLIC_API_URL` at `http://localhost:5000/api`. On the sign-in page the tenant field
   is optional: `tenantadmin` lands in `default` through its only membership, and `admin` signs in
   with no tenant.
7. **Tests and the gate.** `dotnet test src/backend/Tests` needs PostgreSQL reachable with the
   `appsettings.Testing.json` connection string and the `Initial` migration in place; the run
   migrates and seeds `<Name>Test` and deletes it at the end; it needs no Redis. From the root, `npm run gate`
   (`-- --fast` skips the production web build) runs the build, backend tests, web
   lint/typecheck/vitest, the engine and hook tests and `next build`; it runs `npm ci` in the web app
   when `node_modules` is missing.
8. **Before deploying anywhere non-development**, supply `Auth:Jwt:Key` (>= 32 chars),
   `Auth:Jwt:Issuer` and `Auth:Jwt:Audience` through secure configuration — startup refuses to boot
   with the placeholder key outside Development/Testing. `Web:Domains` must list the real front-end
   origins, `Database:ApplyMigrationsOnStartup` defaults to false there (run
   `dotnet ef database update` explicitly), and the seeded accounts' `Admin#123` passwords must be
   changed.

## Then

The generated project's `CLAUDE.md`, `.claude/skills` and `specs/README.md` carry the code guides and
the spec-driven workflow: save a spec under `specs/` and run `npm run loop`, or `npm run queue` /
`npm run auto -- "<task>"`. Review `agentic.config.json` — `project.branch` / `project.baseBranch`,
conventions, departments — since it is the one project-specific file of the loop. This scaffolding
guide and `template-maintenance` are not in the generated project.
