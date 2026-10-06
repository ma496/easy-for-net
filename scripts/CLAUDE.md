# scripts — the engine

Plain Node, no model calls except in one place. This is what makes the loop able to *refuse*:
a deterministic program decides whether a change may commit, and the only non-deterministic
step is the one that writes the code.

```
loop.mjs            the whole cycle: look → observe → plan → drain → report
  agent-queue.mjs   the queue: intake, dependency order, one task at a time
    agent-run.mjs   one task: brief → claude -p → verify → review check → commit
      verify.mjs    the gate: static always, live checks when the diff demands them
```

| File | Does |
|------|------|
| `loop.mjs` | One cycle. Brings up what the cycle needs, observes, plans, drains, then says what needs a human. |
| `agent-queue.mjs` | `todo/ → doing/ → done/`. Plans specs into tasks (committed as `Plan <spec>`), enforces `Depends-on:` against the local lanes and the `Task:` lines in history, drains serially, audits its own bookkeeping, and refuses the base branch under `project.workflow: "team"`. |
| `agent-run.mjs` | One task, up to N attempts. The only file that spawns a model. |
| `verify.mjs` | Decides from the diff what must be checked, and fails when a required check could not run. |
| `test.mjs` | The unit suite, over the roots named in the config. |
| `run-journal.mjs` | Every attempt, its cost, its turns, and why it failed. `npm run auto:status`. |
| `record-build.mjs` | Writes a task's build record into its spec's `docs/builds/<date-time>-<spec>/` (or `adhoc/`). The runner calls it just before committing, so the record is in the task's own commit. |
| `record-lesson.mjs` | Writes a lesson into `.claude/memory/lessons/`, naming the task from `--task` or `AGENT_TASK`. `npm run lessons` lists them. |
| `claude-contract.mjs` | Probes the installed Claude CLI and checks its stream still carries what the runner reads. `--record` saves fixtures; `--if-changed` is what `loop.mjs` runs. |
| `schedule-drain.mjs` | Installs (or removes) the timer that runs the cycle hands-off. |
| `auto-ship.mjs` | Stages deliberately and commits, with the task trailer that lets the audit pair a commit to its brief. |
| `open-pr.mjs` | Prints (or opens) the pull-request page for the current branch. Never merges. |
| `gate.mjs` | The static gate: solution build, backend and tool tests, web lint + `tsc` + vitest, engine and hook tests, web build. `--changed` runs only the steps whose watched paths changed (`lib/gate-scope.mjs` decides). |
| `serve-api.mjs` | Starts the API on `$PORT` as Development — what verify starts for a live check. |
| `dev.mjs` | `npm run dev`: the whole app for a developer — PostgreSQL and Redis through `docker compose` when the probes say they are down, then the API (via `serve-api.mjs`) and `next dev` side by side, each line prefixed. Not part of the loop or the gate. |
| `stop-api.mjs` | Stops this checkout's running API (anything running from `src/backend/Source/bin/`), which on Windows locks the build output. The gate runs it before `dotnet build`; `npm run stop:api`. |
| `pg-ready.mjs` | Whether PostgreSQL accepts connections — the dependency probe for verify and the loop. |
| `redis-ready.mjs` | Whether the Redis in `ConnectionStrings:Redis` accepts a TCP connection — the same probe, for the session store. |
| `smoke.mjs` | The live check: health, the OpenAPI document, and an anonymous caller refused. |
| `deploy-vps.mjs` | `npm run deploy:vps`: puts the app on a Linux server behind Coolify over SSH, from any OS. Uploads `deploy/vps/remote.sh` (POSIX sh, run as root) and runs its `check`/`prepare`/`deploy` phases; the pure parts are `lib/deploy-vps.mjs`. Not part of the loop or the gate, and never run by an agent: `hooks.deniedCommands` refuses it, so only the owner deploys. |
| `lib/` | The pure parts: the department map, spend and budget arithmetic, salvage, memory selection, cross-platform process helpers (`proc.mjs`), the timer's files (`schedule.mjs`), remote and PR URLs (`remote.mjs`), the config and its validation (`project-config.mjs`), the queue lock (`queue-lock.mjs`), `Depends-on:` resolution (`task-deps.mjs`), which tasks history says landed (`landed-tasks.mjs`), the planner's folder and team-intake rules (`planning.mjs`), task scopes, stems and lane listing (`task-names.mjs`), what a runner commit may stage (`stage-paths.mjs`), where a build record goes and what it says (`build-record.mjs`), changed-path listing and the tree fingerprint a passing verify is reused on (`changed-paths.mjs`), the services a task needs before it may start (`dependencies.mjs`), which gate steps a diff reaches (`gate-scope.mjs`), journal outcomes (`outcomes.mjs`), how an attempt ended (`attempt-outcome.mjs`). |
| `lib/claude-events.mjs` | **The only reader of the Claude CLI's stream-json.** `stream-render.mjs` renders and adds up what it normalises. A CLI release that moves a field is fixed here, and `tests/claude-stream-contract.test.mjs` holds it against captured streams. |

**Two kinds of file live here.** The engine — everything above except `gate`, `serve-api`, `dev`, `deploy`,
`stop-api`, `pg-ready`, `redis-ready` and `smoke` — knows nothing about the stack; it asks `lib/project-config.mjs`,
which reads `agentic.config.json`. Those eight are the stack-specific half: they are what the
config's commands name, and they are where a change to how this repository builds or runs
belongs.

**Every child process goes through `lib/proc.mjs`.** `npm` is a `.cmd` shim on Windows, and
`which`, `sleep` and process groups do not exist there; a script that spawns them directly
works on one machine and silently fails on the next.

## Rules

- **No model calls outside `agent-run.mjs`, the planner in `agent-queue.mjs`, and the
  contract probe in `claude-contract.mjs`** — the last on the cheapest model, once per CLI
  version, and never from the gate. Anything else must be able to run on a timer, unattended,
  for free.
- **Every session is started with `resolveModel` / `modelArgs`** from `lib/project-config.mjs`,
  its prompt on stdin, through `lib/proc.mjs`. A session that picks its own model, or takes its
  prompt as an argument, is the one that runs on an unchosen default or fails on Windows'
  command-line limit.
- **Exit codes are the interface.** `0` worked, `1` failed, `3` a spend ceiling stopped it,
  `4` the run never happened (the account cannot run, or a service verification
  needs is down and cannot be started). A caller that cannot tell "out of money" from "never verified"
  will report the wrong thing to a person who is not watching.
- **A required check that could not run is a failure.** Never a pass with a footnote.
- **The scripts never read `.env`.** Ceilings and flags come from the environment the caller
  already has; the timer gets them from the command it was installed with.
- **Pure helpers get unit tests** under the test roots, so `lib/` stays honest. A helper with
  branching logic and no test will be wrong within a month.
