# .claude — Agent Guide

The agentic layer: what agents are available, what they are allowed to do, and what stops
them. Configuration for Claude Code, checked in so the whole team gets the same behaviour.

| Path | Role | Is |
|------|------|-----|
| `settings.json` | Policy | Permissions, hooks, statusline — shared by everyone |
| `agents/` | **Workforce** | Specialised subagents, one per area of the codebase |
| `commands/` | **Shortcuts** | Slash commands: the repeatable loops |
| `skills/` | **Procedures** | Loaded on demand, one per fiddly job |
| `hooks/` | **Guards** | What refuses, on every matching tool call |
| `memory/` | **Learning** | **What the loop learned.** Lessons recorded by past runs, injected into future briefs |
| `statusline.mjs` | Display | Branch, tree state, and drain status in the prompt |

These four directory names are a contract, not a preference: Claude Code finds the agents
because they sit in `agents/`, the skills because they sit in `skills/`. Renaming them does
not rename the framework, it removes it.

`settings.local.json` is gitignored — put personal overrides there, never in `settings.json`.

## The hooks are the real safety net

Documentation asks; hooks enforce. These run regardless of permission mode, which is what
makes an unattended run safe to leave alone.

| Hook | Blocks |
|------|--------|
| `guard-protected-paths.mjs` | Writes to `.env`, to the per-environment `appsettings.*.json`, to build output (`bin/`, `obj/`, `.next/`, `node_modules/`, …), and to anything in `hooks.protectedPaths` |
| `guard-bash.mjs` | For **both** the Bash and PowerShell tools: reading `.env` or the per-environment appsettings through the shell, `dotnet ef database drop`, dropping a Docker volume, `DROP`/`TRUNCATE`/`DELETE`, `git reset --hard`, `git clean -f`, force-push, blind `git add -A`, **pushing to the protected branch by any refspec**, **merging any pull request**, plus every pattern in `hooks.deniedCommands` |
| `project-conventions.mjs` | Nothing — reports the conventions in `hooks.conventions` back to the model after every edit |
| `session-start.mjs` | Nothing — orients a fresh session |

Every hook is a Node script, so it behaves the same under Git Bash, PowerShell and a POSIX
shell, and paths are normalised to forward slashes before a rule sees them — a Windows path
like `D:\repo\.env` is caught exactly as `/repo/.env` is.

`hook-tests.mjs` covers the guards — every rule, both directions — and the gate runs it, so
a hook that throws or has been quietly broadened fails the gate instead of failing silently.

**Every rule carries a block case and a neighbouring allow case.** `cat .env` is blocked and
`cat .env.example` is not; `git push origin HEAD:main` is blocked and
`git push -u origin feat/main-nav` is not. A guard that blocks everything is as useless as
one that blocks nothing, and over-broad regular expressions are how these rules actually
rot. Add both cases when you add a rule.

**Naming a command is not running it.** `guard-bash.mjs` matches the command as text, so two
kinds of text are removed before the rules see it: heredoc bodies, and the search pattern
given to `grep`, `rg`, and friends. Writing a document about `docker compose down -v` and
grepping for `git merge` are both ordinary work. Only the pattern is dropped — the paths
beside it stay — and only when nothing downstream could run what the search prints.

## Delegation is enforced, not suggested

`agent-run.mjs` routes each task to the agents that own its area and **requires every
department that owns part of the finished diff to have seen it**. The map lives in
`agentic.config.json` under `departments`, and `scripts/lib/departments.mjs` reads it.

A green gate proves a change builds and its tests pass; it cannot see a missing isolation
filter, a prompt that invites the model to invent, or an assertion deleted to make the gate
go green — which is exactly what a task under retry pressure is tempted to do.

The requirement is checked rather than trusted: `lib/stream-render.mjs` collects the `Task`
tool calls actually observed in the run's stream, and an attempt missing any owed department
is fed back naming each one and the paths that made it necessary.

The set is derived from the **diff**, not the brief — a task that promised not to touch the
schema and did must still answer to the data engineer.

**Skills are required too.** The same file maps work to the procedures it owes. Loads are
collected from the stream like delegations.

**Design comes before code, and that order is enforced.** A department with `"phase":
"design"` is delegated to *first*, and returns a brief the engineer builds from. It then
appears again in the review tier, checking the built page against what it specified — that
repeat is the design closing its own loop, not a duplicate.

**The rest of the order is enforced too.** Everything that writes finishes before anything
that judges; reviewers sharing a tier run together, because they ask unrelated questions and
running them in sequence spends wall-clock for nothing; the last tier runs once the others
have reported. A reviewer returning CHANGES NEEDED means the run fixes and reviews again, so
the check reads each reviewer's *last* delegation — a repeat is expected, not a violation.

## Memory is the only part that changes by itself

Everything else here is static: the agents, the commands, the skills and the guides all say
the same thing on run one and run two hundred. `memory/` is written *by* runs and read *into*
later ones, which is what stops the loop rediscovering the same trap for ever. The journal
already gives a retry its own task's history; memory is what carries a lesson from one task
to an unrelated one. See [`memory/README.md`](memory/README.md) for the contract, and keep
the bar there high — injection is capped, so every platitude evicts something real.

## Rules

- **Adding a guard is cheap; removing one needs a reason.** If a hook blocks something
  legitimate, narrow its rule — do not delete the hook.
- **Keep agent and command files short and specific.** They are read on every invocation;
  prose costs tokens on every task.
- **Every agent definition ends with how to verify its own work.** An agent that cannot say
  what it checked has not finished.
- **Permissions in `settings.json` are for read-only and gate commands.** Anything that
  writes, pushes, commits, or spawns agents belongs in `ask`, not `allow`.
- **The allow-list is not a safety boundary; the hooks are.** `Bash(cat *)` is allowed
  because prompting for every file read is unusable — which is exactly why `cat .env` has to
  be stopped by `guard-bash.mjs` rather than by a permission entry. When you widen the
  allow-list, ask what the widest command matching it could do, and put the answer in a hook.
