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
| `agent-queue.mjs` | `todo/ → doing/ → done/`. Plans specs into tasks, enforces `Depends-on:`, drains serially, audits its own bookkeeping. |
| `agent-run.mjs` | One task, up to N attempts. The only file that spawns a model. |
| `verify.mjs` | Decides from the diff what must be checked, and fails when a required check could not run. |
| `test.mjs` | The unit suite, over the roots named in the config. |
| `run-journal.mjs` | Every attempt, its cost, its turns, and why it failed. `npm run auto:status`. |
| `record-build.mjs` | Writes a build record per landed task, and rebuilds the index. |
| `record-lesson.mjs` | Writes a lesson into `.claude/memory/lessons/`. `npm run lessons` lists them. |
| `schedule-drain.mjs` | Installs (or removes) the timer that runs the cycle hands-off. |
| `auto-ship.mjs` | Stages deliberately and commits, with the task trailer that lets the audit pair a commit to its brief. |
| `open-pr.mjs` | Prints (or opens) the pull-request page for the current branch. Never merges. |
| `gate.mjs` | The static gate: solution build, backend and tool tests, web lint + `tsc` + vitest, engine and hook tests, web build. |
| `serve-api.mjs` | Starts the API on `$PORT` as Development — what verify starts for a live check. |
| `pg-ready.mjs` | Whether PostgreSQL accepts connections — the dependency probe for verify and the loop. |
| `smoke.mjs` | The live check: health, the OpenAPI document, and an anonymous caller refused. |
| `lib/` | The pure parts: the department map, spend and budget arithmetic, salvage, stream parsing, memory selection, cross-platform process helpers (`proc.mjs`), the timer's files (`schedule.mjs`), remote and PR URLs (`remote.mjs`). |

**Two kinds of file live here.** The engine — everything above except `gate`, `serve-api`,
`pg-ready` and `smoke` — knows nothing about the stack; it asks `lib/project-config.mjs`,
which reads `agentic.config.json`. Those four are the stack-specific half: they are what the
config's commands name, and they are where a change to how this repository builds or runs
belongs.

**Every child process goes through `lib/proc.mjs`.** `npm` is a `.cmd` shim on Windows, and
`which`, `sleep` and process groups do not exist there; a script that spawns them directly
works on one machine and silently fails on the next.

## Rules

- **No model calls outside `agent-run.mjs` and the planner in `agent-queue.mjs`.** Anything
  else must be able to run on a timer, unattended, for free.
- **Exit codes are the interface.** `0` worked, `1` failed, `3` a spend ceiling stopped it,
  `4` the account cannot run. A caller that cannot tell "out of money" from "never verified"
  will report the wrong thing to a person who is not watching.
- **A required check that could not run is a failure.** Never a pass with a footnote.
- **The scripts never read `.env`.** Ceilings and flags come from the environment the caller
  already has; the timer gets them from the command it was installed with.
- **Pure helpers get unit tests** under the test roots, so `lib/` stays honest. A helper with
  branching logic and no test will be wrong within a month.
