---
name: template-maintenance
description: Work on the EasyForNet template repository itself — how src/ ships to generated projects, the dotnet efn CLI in tool/, the version/tag coupling, the root-file copy list, and the publish flow. Use when changing the generator, adding root-level files, or releasing a new version.
---

# Maintaining the template and the CLI

This repository holds two coupled deliverables:

- `src/` — the template: the API (`src/backend`) and the web app (`src/frontend/web`). It is a
  working application, but its primary job is to be copied into new projects.
- `tool/EasyForNetTool` — the `dotnet efn` CLI, packaged as `EasyForNetTool` with command
  `dotnet-efn`.

## The version ↔ tag contract

`efn cp` clones the template repo into `%LOCALAPPDATA%/EasyForNet/Templates/<tool version>` and
checks out the tag **`v{tool version}`**. So a published tool version only works if a matching tag
exists on GitHub. `publish-package.sh` enforces the order: clean working tree → run the tool tests →
`dotnet pack` → create and push tag `v$VERSION` → `dotnet nuget push`. The version lives in
`tool/EasyForNetTool/EasyForNetTool.csproj` (`<Version>`) and is set by `-p:Version` at pack time.

Practical consequence: **anything you change under `src/` only reaches users after the next tagged
release.**

## Keep the template generic

Changes under `src/` land in every scaffolded project. No project-specific names, no hardcoded
domains, no sample business entities. `Identity`, `FileManagement` and `Notifications` are the
intended baseline; add to them only what every new project would want.

## What gets copied — and what does not

`tool/EasyForNetTool/Generator/CreateProjectGenerator.cs` copies an **explicit** list:

- `src/backend` (recursively, excluding `Migrations`) and `src/frontend/web`
- `appsettings.json` duplicated into `appsettings.Development.json` / `appsettings.Testing.json`
- `.env.example` copied to `.env.development` (the real file is git-ignored)
- root files `.editorconfig`, `.gitignore`, `global.json`
- directories `.config`, `.vscode`, and `.claude` (minus the `new-project` and `template-maintenance`
  skills, which describe working on this repository rather than on a generated project)
- `CLAUDE.md`, written from the embedded resource `tool/EasyForNetTool/new-project-claude.md`

**A new root-level file or directory reaches generated projects only if it is added to that list.**
`specs/` is deliberately absent — each project accumulates its own specifications.

**Inside `.claude` the rule inverts.** `CopyDirectory` excludes by directory *name*, recursively, so
everything under `.claude` ships unless its directory is named `new-project` or `template-maintenance`
at some level. `.claude/skills`, `.claude/workflows` and `.claude/commands` all reach generated
projects with no generator change. To keep something template-only, add its directory name to that
array or put it outside `.claude`.

Text inside copied markdown is rewritten (`Backend.` → the project's root namespace,
`EasyForNet.slnx` → `<Name>.slnx`) — keep namespace references in `.claude` markdown and
`new-project-claude.md` in that `Backend.`-qualified form so the rewrite catches them, and avoid the
bare word "Backend" in prose.

**`ReplaceInFiles` filters on an exact extension, and it is only ever called with `.md`.** A `.js`,
`.json` or `.ts` file under `.claude` is copied byte-for-byte. So the dynamic-workflow scripts in
`.claude/workflows` must contain no namespace, no solution file name and no project file name; they
delegate anything repo-specific to `CLAUDE.md` and to `.claude/skills/spec-driven/SKILL.md`, which are
rewritten. `rg "Backend\.|EasyForNet|\.slnx|\.csproj" .claude/workflows` must return nothing.

Renaming happens through `NamespaceRewriter` (Roslyn) for `.cs` files and regex `ReplaceInFile` /
`ReplaceInFiles` for everything else, including `Meta.cs`'s `InternalsVisibleTo`,
`FeatureDependencyTests.cs`, the `.csproj` names, `package.json` / `package-lock.json` and the app
display name.

## Migrations

`CopyDirectory(..., ["Migrations"])` skips the template's migrations on purpose — generated
projects run `dotnet ef migrations add Initial` themselves. When you change an entity here, add a
migration to this repo (so the template app still runs), and remember new projects will fold the
change into their own `Initial`.

## Documentation that ships

Two docs describe the architecture: this repo's `CLAUDE.md` and the embedded
`tool/EasyForNetTool/new-project-claude.md` that becomes the generated project's `CLAUDE.md`, plus
the skills under `.claude/skills`. When an architectural rule changes, update all of them — they
drift silently otherwise. The generated project's `CLAUDE.md` is a separate file, so a section added
to this repo's `CLAUDE.md` does not reach new projects until it is added there too. Keep them purely
operational: no provenance or origin statements.

## Commands

```sh
dotnet test tool/EasyForNetTool.Tests/EasyForNetTool.Tests.csproj
dotnet build EasyForNet.slnx
./publish-package.sh                 # interactive: version prompt + NuGet publish confirmation
```

The generator itself has no automated test (it shells out to `git clone`), so verify changes by
running the built tool against a scratch directory and inspecting the output — build the generated
solution and start both apps before publishing.
