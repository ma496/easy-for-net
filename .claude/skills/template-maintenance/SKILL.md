---
name: template-maintenance
description: Work on the EasyForNet template repository itself — how src/ and the agentic layer ship to generated projects, the dotnet efn CLI in tool/, the copy and rewrite rules, locale filtering, the version/tag coupling, testing the generator locally, and the publish flow. Use when changing the generator, adding root-level files or anything under .claude, or releasing a new version.
---

# Maintaining the template and the CLI

This repository holds two coupled deliverables:

- `src/` — the template: the API (`src/backend`) and the web app (`src/frontend/web`). It is a
  working application, but its primary job is to be copied into new projects, together with the
  agentic layer at the root (`.claude`, `scripts/`, `agentic.config.json`, the task-loop skeleton).
- `tool/EasyForNetTool` — the `dotnet efn` CLI, packaged as `EasyForNetTool` with command
  `dotnet-efn`. One command: `createproject` / `cp` with `-n/--name`, `-o/--output`,
  `-m/--multilanguage` (options are defined in `ArgumentInfo.cs`, parsed in `Parsing/`).

## The version ↔ tag contract

`efn cp` clones the template repo into `%LOCALAPPDATA%/EasyForNet/Templates/<tool version>` and
checks out the tag **`v{tool version}`** (the version is the `major.minor.patch` of the assembly's
informational version). A published tool version only works if a matching tag exists on GitHub, and
an existing cache directory is reused without fetching.

Practical consequence: **anything you change under `src/`, `.claude`, `scripts/` or the root files
only reaches users after the next tagged release** — and the tool never reads this working tree.

## Keep the template generic

Everything copied lands in every scaffolded project. No project-specific names, no hardcoded
domains, no sample business entities. `Identity`, `Tenancy` (with feature management),
`Settings`, `Localization`, `FileManagement` and `Notifications` are the baseline; add to them only what every new project
would want.

## What gets copied — and what does not

`tool/EasyForNetTool/Generator/CreateProjectGenerator.cs` copies an **explicit** list:

- `src/backend` recursively, excluding every directory named `Migrations`, and `src/frontend/web`
  recursively (whatever is on disk in the tagged clone — only tracked files, since it is a fresh
  clone)
- `appsettings.json` duplicated into `appsettings.Development.json` / `appsettings.Testing.json`
- `src/frontend/web/.env.example` copied to `.env.development`
- the root `.env` written from `.env.docker.example` by `WriteDockerEnvAsync`: the project's Compose
  project, database and Redis key prefix, `DOMAIN=localhost` / `PUBLIC_URL=https://localhost`, and
  fresh random database, Redis and administrator passwords and JWT key, and `DEV_POSTGRES_PORT` /
  `DEV_REDIS_PORT` (a key added to the example ships as it is unless it is named there)
- the development ports: `DevPorts.Find(DevPorts.IsFree)` keeps 5432 and 6379 when nothing on the
  machine holds them and otherwise takes the next free port above each, and the same pair goes to
  `WriteDockerEnvAsync` and `CustomizeAppSettingsAsync` so `.env` and every connection string
  (PostgreSQL, Hangfire, Redis) agree
- root files `.editorconfig`, `.gitignore`, `.gitattributes`, `global.json`, `package.json`,
  `agentic.config.json`, `docker-compose.yml`, `docker-compose.prod.yml`, `docker-compose.coolify.yml`
  (what `npm run deploy:vps` points Coolify at), `.env.docker.example`
  (`CopyFiles` throws if one is missing)
- directories `.config`, `.vscode`, `docker`, `scripts` (whole), and `.claude` minus any directory named
  `new-project`, `template-maintenance` or `lessons`
- `CLAUDE.md` and `README.md`, written from the embedded resources
  `tool/EasyForNetTool/new-project-claude.md` and `new-project-readme.md` (`{{Name}}` / `{{name}}`
  replaced with the project name)
- **the task loop's records as an empty skeleton** (`CopyTaskLoopSkeleton`): `specs/README.md`,
  `specs/TEMPLATE.md`, `docs/AGENTIC_WORKFLOW.md`, `docs/capabilities/README.md`,
  `.agent-queue/planned.json` as `{}`, and `.claude/memory/lessons/.gitkeep`. The queue creates its
  lanes itself (`doing/`, `done/` and `failed/` are per machine, gitignored by the copied
  `.gitignore`), and `docs/builds/` appears with the first landed task;
  `CopyTaskLoopSkeletonTests` pins the exact file list.

Not copied: this repository's `README.md`, `LICENSE`, `EasyForNet.slnx` (a new `<Name>.slnx` is created with
`dotnet new sln -f slnx` and both `.csproj` files added), `publish-package.sh`, `tool/`, and this
repository's own specs, build records, queued tasks and lessons.

Last, the generator runs `git init`, `git add .` and `git commit -m "Initial project"` in the new
project, since the task loop needs a repository and a clean tree; no git, or a refused commit (no
`user.name`), is reported and generation still succeeds.

**A new root-level file or directory reaches generated projects only if it is added to that list.**
A new file under `scripts/` ships automatically; one under `specs/`, `docs/` or `.agent-queue/`
does not unless `CopyTaskLoopSkeleton` names it.

**Inside `.claude` the rule inverts.** `CopyDirectory` excludes by directory *name* at any depth, so
anything added under `.claude` — a new skill, agent, command or hook — ships with no generator
change. To keep something template-only, add its directory name to that exclusion array (and pick a
name no shipped directory uses) or put it outside `.claude`.

## What gets rewritten

- `.cs` files in `Source` and `Tests`: `NamespaceRewriter` (Roslyn) replaces the root namespace
  (`<RootNamespace>` from the `.csproj`, else the project file name) in qualified and identifier
  names, then the file is reformatted. Names are compared by prefix.
- `Backend.csproj` → `<Name>.csproj`, `Backend.Tests.csproj` → `<Name>.Tests.csproj` and the test
  project's reference to it; `Meta.cs`'s `InternalsVisibleTo`; `Program.cs`'s
  `c.Binding.ReflectionCache.AddFrom<ProjectName>` call; the namespace strings in
  `Tests/Architect/FeatureDependencyTests.cs`.
- `CustomizeAppSettingsAsync` rewrites the connection strings (`ConnectionStrings.DefaultConnection`,
  `Hangfire.Storage.ConnectionString`) to `Database=<name>` (`<name>_test` for Testing, `<name>` being the snake_case project name) — with a
  literal `{password}` in `appsettings.json` and `postgres` (the `docker-compose.yml` default) in
  Development/Testing — sets `Redis.InstanceName` to `<name>:` (`<name>_test:` for Testing), gives
  Development/Testing a GUID `Auth.Jwt.Key`, and writes one random password into
  `Seed.PlatformAdminPassword` / `Seed.TenantAdminPassword` of all three files. These are JSON-path
  updates, so renaming or moving those keys in `appsettings.json` must be mirrored in the generator.
- `name` in the web `package.json` / `package-lock.json`, the root `package.json`, and
  `project.name` in `agentic.config.json` become the kebab-case name.
- The display name: `Easy\s+For\s+Net` in every `.json` under the web app and in the API's shipped
  translation resources (`Features/Localization/Core/Resources/*.json`) becomes the name in title
  case.
- Markdown under `.claude`: `Backend.` → `<Name>.` and `EasyForNet.slnx` → `<Name>.slnx`. Keep
  namespace references there `Backend.`-qualified so the rewrite catches them (a sentence ending in
  the bare word "Backend" also matches).
- The generated `CLAUDE.md` gets **only** the `EasyForNet.slnx` rewrite, so
  `new-project-claude.md` must not name `Backend.` namespaces or `Backend.csproj`; it uses
  generic forms (`<RootNamespace>.Features.X`, `--project src/backend/Source`).

Everything else is byte-for-byte: non-markdown files under `.claude`, `scripts/`,
`agentic.config.json`, `specs/`, `docs/`. Keep namespaces, the solution file name and project file
names out of them — they address the solution by globbing the root `*.slnx` and the API by directory
(`src/backend/Source`, `src/backend/Tests`).

## Locale filtering

Translations ship as the API's resource files, so without multi-language the generator deletes
`{ur,zh,ar,hi,es,fr,ru}.json` from `src/backend/Source/Features/Localization/Core/Resources` and
regex-rewrites one literal to English only: `locales: [...]` in `i18n/config.ts`; it also drops every
culture but `en` from the web app's `i18n/offline-resources.json` (`KeepOfflineResourceLocalesAsync`).
Changing the shape
of that literal makes the regex miss silently (and `i18n/locales.test.ts` then fails in the generated
project); adding or removing a shipped language means updating the generator's list too.
`LanguageCatalog` is left whole — an entry with no resource file is harmless.

`-m`/`--multilanguage` takes a value: `-m true` keeps every language, `-m false` (the default) is
English-only, and anything else is refused. The option names `CreateProjectArgument.MultiLanguage`
through its `PropertyName`, since the PascalCase of the option name would not match.

## Migrations

`CopyDirectory(..., ["Migrations"])` skips the template's migrations on purpose — generated
projects run `dotnet ef migrations add Initial` themselves. When you change an entity here, add a
migration to this repo (so the template app still runs); new projects fold the change into their
own `Initial`. Data a migration inserts (rather than the seeder) never reaches a generated project.

## Seeded first run

`ShareData/DataSeeder` runs on every start in every environment and seeds, when absent: the
**Default** tenant (identifier `default`), the platform account `admin` (no
membership) and the Default tenant's administrator `tenantadmin`, with the passwords in the `Seed`
section of `appsettings.json` (`Admin#123` here; `CustomizeAppSettingsAsync` writes one random
password into all three appsettings files of a generated project). Tests sign in with
these through `TestUsers.AdminPassword` / `PlatformAdminPassword`, and `new-project`, `CLAUDE.md` and `new-project-claude.md` document them — change them in
all places together.

## Documentation that ships

The architecture is described twice: this repo's `CLAUDE.md` and the embedded
`tool/EasyForNetTool/new-project-claude.md` that becomes the generated project's `CLAUDE.md`, plus
the skills under `.claude/skills`. **Any architecture change written into `CLAUDE.md` must also be
written into `new-project-claude.md`** — it is a separate file, and nothing copies one into the
other. It differs from `CLAUDE.md` on purpose in: its opening (project layout rather than this
repository), generic namespace forms, no tool commands, the git-init note for the task loop, and no
`new-project` / `template-maintenance` entries in its skill list. Keep all of them purely
operational: no provenance or origin statements. The embedded file is compiled into the tool, so a
change to it ships with the next package, not the next tag alone.

## Commands

```sh
dotnet test tool/EasyForNetTool.Tests/EasyForNetTool.Tests.csproj
dotnet build EasyForNet.slnx
npm run gate                         # also runs every tool/*.Tests project it finds
./publish-package.sh                 # interactive: version prompt + NuGet publish confirmation
```

The tool tests cover argument parsing, string helpers, `Helpers`, `NamespaceRewriter`, and the
generator's file rewrites (`CustomizeAppSettingsAsync`, `WriteDockerEnvAsync`,
`KeepOfflineResourceLocalesAsync`); `Generate` itself has no automated test (it shells out to `git`
and `dotnet`).

### Testing the generator locally

The tool always copies from the cache for its version, never from this working tree. To run a
change end to end, commit it on a branch, then seed a cache for a version that has no tag:

```sh
rm -rf "$LOCALAPPDATA/EasyForNet/Templates/0.0.0"
git clone . "$LOCALAPPDATA/EasyForNet/Templates/0.0.0"
git -C "$LOCALAPPDATA/EasyForNet/Templates/0.0.0" checkout <your-branch>
dotnet run --project tool/EasyForNetTool --property:Version=0.0.0 -- cp -n scratch-app -o <scratch dir>
```

Then build the generated solution, add the `Initial` migration, and start both apps before
publishing.

## Publishing

`publish-package.sh`, from the repository root:

1. Refuses a dirty working tree.
2. Proposes the latest tag's patch + 1 as the version (override at the prompt). The `<Version>` in
   `EasyForNetTool.csproj` is not changed by the script; it is what local builds report.
3. Runs the tool tests (Release), then `dotnet pack` with `-p:Version` / `-p:PackageVersion`.
4. Asks for confirmation; declining stops before anything is tagged.
5. Creates tag `v$VERSION` at `HEAD` (reuses it if it already points at `HEAD`, fails if it points
   elsewhere) and pushes it to `origin`.
6. `dotnet nuget push` to nuget.org, with `--api-key "$NUGET_API_KEY"` when that variable is set.

The tag is pushed before the package so that no published version lacks its template.
