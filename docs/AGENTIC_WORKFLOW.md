# The agentic workflow

How work gets done here without a person driving each step, and what each part refuses to do.

---

## The shape of it

```
specs/*.md            somebody describes what they want
   │  npm run queue -- plan        (a model splits it into tasks)
   ▼
.agent-queue/todo/    one task per file, in dependency order
   │  npm run queue -- drain       (serial, one at a time)
   ▼
agent-run.mjs         brief → claude -p → verify → review check → commit
   │
   ▼
.agent-queue/done/    the brief, filed, with a build record naming its commit
docs/builds/          the durable record of what shipped and why
.agent-runs/          every attempt, its cost, its turns, why it failed
.claude/memory/       what a run learned, injected into later unrelated tasks
```

`npm run loop` is all of it in one command, and `npm run schedule -- install` runs that on a
timer.

## Work reaches the loop two ways

**You write a spec.** Save a markdown file into `specs/`. The next drain splits it into tasks
and queues them — so saving the file *is* starting development. Intake is keyed on content
hash in `.agent-queue/planned.json`: a spec is planned once, re-planned if edited.

**The product files one.** Configure `cycle.observe` with a command that reads the running
product — logs, error rates, complaints — and writes a task brief into `specs/` when a
pattern crosses a threshold. It files a problem, never a solution, and it must be cheap
enough to run on a timer.

## Build order is enforced, not suggested

Tasks run one at a time and each commits before the next starts, so a task sees every task
that ran before it and none that has not run yet. A task declares what it needs under its
subject line:

```markdown
Build the reporting dashboard
Depends-on: 01-reporting-data-contract
```

A dependency is satisfied once that task is in `done/`, which means its code is committed —
so the chain advances on its own. Two tasks editing the same file are still not independent:
give one a `Depends-on:` on the other, or the later one will be working from the earlier
one's committed result without knowing it.

Tasks run strictly serially in one checkout. `--parallel` is refused rather than quietly
ignored, because two tasks sharing a working tree would overwrite each other.

## What a task must survive

1. **The gate.** Typecheck, unit tests, hook tests, build — whatever `verify.gate` names.
2. **The live checks its diff demands.** From `verify.checks`. A required check that could
   not run counts as a failure, not as a pass with a footnote.
3. **A non-empty diff.** Verification passing over an untouched tree is the gate proving the
   branch is green, not the task being done. An attempt that wrote nothing is refused.
4. **Every department that owns part of the diff.** Derived from the finished diff, not from
   the brief's prose, and read from the delegations actually observed in the run's stream —
   not from the session's own account of what it did.
5. **Every skill that work owed.** Same mechanism.

Only then may it commit.

## Retries, salvage, and why the third attempt is not waste

A failed attempt's work is parked under `.agent-runs/interrupted/<task>/` rather than
discarded. The next run **restores it and verifies first**: green means the session only has
to review, red means it only has to fix. Rebuilding from zero is what it avoids, and that is
most of the cost of a retry.

Three attempts is the default (`budget.attempts`), and it is worth measuring with
`npm run auto:status` before changing. A large share of landed work lands on the last attempt,
and cutting one does not save that attempt's money: the task then fails, is requeued, and
starts again from nothing.

## Spend is bounded, not merely discouraged

| Setting | Bounds |
|---------|--------|
| `AGENT_MAX_USD_PER_TASK` | One task across all of its attempts **and all of its runs** — read back from the journal, so a task the drain keeps restarting cannot start each run's counter at zero. |
| `AGENT_MAX_RUNS_PER_TASK` | How often a brief may be started at all before it goes to `failed/` for a person rather than back to `todo/` for the timer. |
| `AGENT_MAX_USD_PER_DRAIN` | A whole drain, planning included. Checked between tasks only — a task killed halfway leaves a dirty tree, which is worse than letting it finish. |

All three refuse rather than warn, because a warning on an overnight timer is read the next
morning. The per-task ceiling stops the *retrying*: the attempt already paid for finishes and
is accepted if it verifies, and the run exits 3 with nothing committed, so "ran out of money"
never reads as "never verified".

`npm run queue -- retry` puts a failed task back, and clears its spend history in the same
gesture, because requeueing by hand is the one signal that somebody looked.

## The model the session runs on is the largest single cost

Every subagent declares its model in `.claude/agents/*.md`. The session that spawns them,
reads the codebase, writes the code and runs the commands takes `AGENT_MODEL`. Measure before
choosing: a weaker session can be both slower and more expensive, because it spends more
turns reaching the same place — and turns are both the clock and the bill.

`npm run auto:status` records turns and cost per attempt, which is what makes this
answerable rather than arguable.

## What is never automated

- **Pushing**, unless the operator sets `AGENT_AUTO_PUSH=1` deliberately.
- **Merging a pull request.** No script, agent, or API call here does it, and the hooks
  block every route.
- **Anything with blast radius the operator did not ask for** — dropping or rewriting a
  database, editing the per-environment settings that hold credentials, spending outside the
  ceilings.

## What a run needs on this machine

- **Node 24+**, **git**, the **.NET 10 SDK**, and the **`claude` CLI** on PATH.
- **PostgreSQL running** where `appsettings.Testing.json` points. The gate runs the backend
  integration tests, which migrate and seed their own database; with the server down every
  task fails its gate, and `npm run loop` says so in its preflight.
- `src/frontend/web/node_modules` — the gate runs `npm ci` there on a fresh checkout.

## The queue repairs its own bookkeeping

A run's commit carries a `Task: <brief-stem>` trailer, so `npm run queue -- audit` can tie a
brief to its commit even when someone committed the work by hand under a different message.
`audit --fix` then files it: the brief moves to `done/` with its build record, and the tasks
waiting on it are released. It acts only on a definite match, and it deliberately never
touches a `done/` task with no commit behind it — choosing between requeueing and deleting
that brief is a judgement, not bookkeeping.
