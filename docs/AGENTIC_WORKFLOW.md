# The agentic workflow

How work gets done here without a person driving each step, and what each part refuses to do.

---

## The shape of it

```
specs/*.md            somebody describes what they want
   │  npm run queue -- plan        (a model splits it into tasks; committed as "Plan <spec>")
   ▼
.agent-queue/todo/    one task per file, in dependency order — shared through git
   │  npm run queue -- drain       (serial, one at a time)
   ▼
agent-run.mjs         brief → claude -p → verify → review check → commit
   │
   ▼
docs/builds/          the durable record of what shipped and why, in the task's own commit
.agent-queue/done/    the brief, filed on this machine (local, gitignored)
.agent-runs/          every attempt, its cost, its turns, why it failed
.claude/memory/       what a run learned, injected into later unrelated tasks
```

`npm run loop` is all of it in one command, and `npm run schedule -- install` runs that on a
timer.

## Work reaches the loop two ways

**You write a spec.** Save a markdown file into `specs/`. The next drain splits it into tasks
and queues them — so saving the file *is* starting development; an uncommitted spec does not
count as a dirty tree. Intake is keyed on the spec's content hash (line endings ignored), kept
in one record per spec, `.agent-queue/planned/<spec>.md.json` (a legacy shared
`planned.json` is still read): a spec is planned once, re-planned if edited. The planner's
briefs, the spec and its record are committed together as `Plan <spec>`, so every other
checkout of the branch sees the spec as planned — and two branches planning different specs
never touch the same file. To read the split before anything is built, run
`npm run queue -- plan` first; the next drain builds what it queued.

Re-planning an edited spec withdraws that spec's tasks still waiting in `todo/` to
`.agent-runs/superseded/` and asks the planner to replace them (they come back if planning
fails); tasks that already landed stay. A spec still uncommitted after intake — its planning
failed, or ran out of budget — stops the drain rather than riding into a task's commit. The
planner runs bounded (`budget.planMaxTurns`, `planMaxUsd`, `planMaxMinutes`), with no shell;
a plan that edits anything outside the queue is refused and its edits moved to
`.agent-runs/rejected-plans/`, and one version of a spec that fails `budget.planAttempts`
times is not planned again until it is edited.

Each spec's tasks live in a folder named for the spec, in every lane:
`.agent-queue/todo/billing-export/01-endpoint.md`. A task's identity is `<spec>/<name>` —
what its commit's `Task:` line carries — so every spec numbers from `01-` without two tasks
ever sharing a name. Planning an edited spec again writes into the same folder and numbers on
from the highest name it already used there, queued or landed. A planning call that writes
anywhere but its own folder, names a file other than `NN-short-slug.md`, or reuses a name
queues nothing. A task queued by hand lives in `adhoc/`.

Each planning also names a build-record directory, `docs/builds/<date-time>-<spec>/`, stamped
when it started and kept in the spec's plan record; that spec's tasks record into it.

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

A bare name is the sibling in the same plan; `<scope>/<name>` reaches a task of another plan.
A dependency is satisfied once a commit reachable from HEAD names that task on its `Task:`
line (or it is in this machine's `done/`), which means its code is committed — so the chain
advances on its own, and a teammate's checkout or a fresh clone agrees about it. Two tasks editing the same file are still not independent:
give one a `Depends-on:` on the other, or the later one will be working from the earlier
one's committed result without knowing it.

Tasks run strictly serially in one checkout. `--parallel` is refused rather than quietly
ignored, because two tasks sharing a working tree would overwrite each other.

## What a task must survive

1. **The gate.** Typecheck, unit tests, hook tests, build — whatever `verify.gate` names. Here
   that is `gate.mjs --changed`: the steps the diff's paths can break, all of them when a path
   is watched by none. `npm run verify -- --full` runs every step.
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
gesture, because requeueing by hand is the one signal that somebody looked. It does so at
most twice per brief (counted in the gitignored `.agent-queue/retries.json`); past that, the
brief needs changing, not another run — edit it, or delete its line from that file to allow
another retry.

### Every setting read from the environment

The scripts never read `.env`; these come from the shell that starts them, and the timer
gets them from the command `npm run schedule -- install` wrote.

| Variable | Default | What it does |
|---|---|---|
| `AGENT_MAX_USD_PER_TASK` / `_PER_DRAIN` | 50 / 200 | The spend ceilings above; `off`, `none`, `unlimited` or `infinity` removes one. |
| `AGENT_MAX_RUNS_PER_TASK` | 3 | Starts of one brief before it goes to `failed/`. |
| `AGENT_MAX_TURNS_PER_ATTEMPT` | `budget.maxTurns` | Parent turns per attempt (`--max-turns`). |
| `AGENT_MAX_MINUTES_PER_ATTEMPT` | `budget.maxMinutesPerAttempt` | Wall clock per attempt. |
| `AGENT_VERIFY_TIMEOUT_MINUTES` | 60 | How long one verify may run before it is stopped and counted as failed. |
| `AGENT_ASSUMED_USD_PER_CALL` | learned from history, else 1 | What a call whose stream carried no cost is charged at. |
| `AGENT_MODEL` | `budget.model` | The model every session and the planner run on; `inherit` passes none. |
| `AGENT_AUTO_PUSH` | 0 | `1` pushes the work branch after each landed task. |
| `AGENT_REFUSE_DIRTY_START` | 1 | `0` lets a drain park a dirty tree under `.agent-runs/interrupted/` instead of refusing. |
| `AGENT_WORK_BRANCH` | — | Set by the timer: the branch it was installed on. |
| `AGENT_SKIP_CLI_CONTRACT` | 0 | `1` skips the loop's once-per-CLI-version contract check. |

The runner itself sets `AGENT_RUN_ID` (the session's run; inside one, `verify` starts the API
when a live check needs it), `AGENT_NO_AUTOSTART` (from `--no-autostart`) and
`AGENT_FROM_DRAIN` (a drain started this run, so anything in the tree is its salvage).

The flags worth knowing: `queue -- drain --max N` (stop after N tasks), `--no-plan` (build
without intake); `loop --dry-run`; `auto --salvage` (take over uncommitted work already in the
tree — otherwise a manual run refuses it), `--no-preflight`, `--no-autostart`, `--safe`;
`schedule -- install --interval <seconds>`, `--auto-push`; `queue -- resume` (requeue what a
dead run left in `doing/`, its leftovers parked for its next run), `retry`, `audit [--fix]`.

## The model the session runs on is the largest single cost

Every subagent declares its model in `.claude/agents/*.md`. The session that spawns them,
reads the codebase, writes the code and runs the commands takes `budget.model`, overridden by
`AGENT_MODEL`; the planner takes the same. All of them default to `opus`, the alias that always
names the newest Opus. Measure before choosing otherwise: a weaker session can be both slower
and more expensive, because it spends more turns reaching the same place — and turns are both
the clock and the bill.

## The CLI's output is a contract

Everything the runner decides about a session is read out of `claude -p --output-format
stream-json`: the cost from the `result` event, the parent's turns (distinct `message.id`s
on events whose `parent_tool_use_id` is null — a subagent's events share the parent's
session id), the delegations and skill loads from `Agent`/`Task` and `Skill` tool calls, a
limit stop from the result's `subtype`. That format belongs to the CLI and moves between
releases, so it is read in exactly one file, `scripts/lib/claude-events.mjs`.

- `scripts/tests/claude-stream-contract.test.mjs` replays streams captured from real CLI
  versions (`scripts/tests/fixtures/claude-stream/<version>/`) and fails naming the field
  that moved.
- `npm run test:claude-contract` runs short probes against the installed CLI on the cheapest
  model; `npm run loop` runs it once per new CLI version and builds nothing when it fails.
- After adapting `claude-events.mjs`, `npm run test:claude-contract -- --record` saves the new
  version's fixtures, scrubbed of paths and content.
- Each attempt's raw stream is kept in `.agent-runs/<run>-attempt-N.stream.jsonl`.

`npm run auto:status` records turns and cost per attempt, which is what makes this
answerable rather than arguable.

## What is never automated

- **Pushing**, unless the operator sets `AGENT_AUTO_PUSH=1` deliberately.
- **Merging a pull request.** No script, agent, or API call here does it, and the hooks
  block `git merge` (however git's global options are spelled around it), `gh pr merge` and
  the hosting APIs' merge endpoints.
- **Anything with blast radius the operator did not ask for** — dropping or rewriting a
  database, editing the per-environment settings that hold credentials, spending outside the
  ceilings.

## What a run needs on this machine

- **Node 24+**, **git**, the **.NET 10 SDK**, and the **`claude` CLI** on PATH.
- **PostgreSQL running** where `appsettings.Testing.json` points. The gate runs the backend
  integration tests, which migrate and seed their own database; with the server down every
  task fails its gate, and `npm run loop` says so in its preflight.
- **Redis running** where `ConnectionStrings:Redis` points, for the live check's API.
- The runner probes both before an attempt starts and, when one is down and cannot be
  started, exits 4 having spent nothing; the drain puts the task back in `todo/` and stops.
- `src/frontend/web/node_modules` — the gate runs `npm ci` there on a fresh checkout.

## The queue repairs its own bookkeeping

A run's commit carries a `Task: <brief-stem>` trailer, so `npm run queue -- audit` can tie a
brief to its commit even when someone committed the work by hand under a different message.
`audit --fix` then files it: the brief moves to `done/`, its removal from `todo/` is committed
(except on a team's base branch), and the tasks waiting on it are released. It acts only on a
definite match, and it deliberately never touches a `done/` task with no commit behind it —
choosing between requeueing and deleting that brief is a judgement, not bookkeeping.

## Working alone or in a team

`project.workflow` in `agentic.config.json` says which, and the rest of the loop is the same
either way.

**`solo`** (the default). One developer. The queue plans and builds on whatever branch is
checked out — the base branch included — and the commits stay local until you push them.
The guard refuses an agent's push to a protected branch (`main`, `master`, `develop`, the base,
`project.branch`) whatever is said in the turn: you push it yourself, `! git push` in the
prompt. Interactively, `/ship` verifies, asks `npm run owes` which reviewers the diff owes,
reviews and commits — the same bar the queue holds a task to. A timer is pinned to the branch it
was installed on in `solo` too (see below), so checking out another branch pauses it rather
than moving the queue there.

**`team`**. Several developers share the base branch, so nothing plans or builds on it:
`queue plan`, `queue drain`, `loop`, `auto` and `schedule install` all refuse there. The rule is
**one spec, one owner, one branch, one pull request**:

```sh
git switch -c feat/billing-export
# write specs/billing-export.md
npm run loop                         # plans it (one "Plan" commit), builds each task (one commit each)
git push -u origin HEAD              # yours to run; the loop never pushes unless AGENT_AUTO_PUSH=1
npm run pr                           # prints the pull-request URL for the pushed branch
```

Intake on a branch plans only the specs that branch added or changed since it left the base
branch, so a spec that arrived from someone else's merged work is never planned a second
time — and a drain builds only the briefs this branch added or changed, so a teammate's task
that was still queued when their pull request merged is left for them, and listed. The guard
also refuses a commit on the base branch itself, and the interactive commands (`/feature`,
`/auto`, `/ship`) branch off it first.

A timer belongs to a checkout, and is pinned to the branch it was installed on
(`AGENT_WORK_BRANCH`): a cycle that fires while another branch is checked out refuses rather
than building there. One timer per checkout; switching branches means reinstalling it.

**What git shares and what stays on one machine:**

| Path | |
|---|---|
| `specs/`, `.agent-queue/todo/`, `.agent-queue/planned/` | Tracked — the queue's inputs |
| `docs/builds/` | Tracked — one record per landed task, in that task's commit |
| `.agent-queue/doing/`, `done/`, `failed/` | Local — this machine's progress, gitignored |
| `.agent-runs/` | Local — journals, streams, parked work |

A task's commit is its code, the removal of its own brief from `todo/` and its build record,
nothing more. A
task that fails leaves its brief's removal unstaged, so it never rides along in another
task's commit; `queue retry` puts it back.

**Any merge style works, if the commit bodies survive it.** "Landed" is read from the
`Task:` lines in history, never from a recorded commit hash, so a branch merged with a merge
commit, rebased, or squashed still reads as landed on the base branch — provided the squash
message keeps each commit's body. GitHub's default squash message ("Default message" /
"commit details") does; the "Pull request title and description" setting drops the `Task:`
lines, and the tasks then read as never landed. Keep the default, or paste the `Task:` lines
into the pull-request description.
Each task's build record is written just before its commit and lands in it:
`docs/builds/<date-time>-<spec>/<task>.md`, one directory per planned spec, named for when planning started
(`docs/builds/adhoc/<date-time>-<task>.md` for a task queued by hand). A record cannot name
its own commit's hash, so it names the task, and `git log --grep "Task: <stem>"` finds the
commit. Records of different specs live in different directories, so two pull requests never
conflict over them.
