# specs — where work is described

**Drop a markdown file in here and you are done.** Saving a file into `specs/` is the whole
act of starting development. The next drain reads anything new, splits it into individual
tasks, works out what has to be built before what, and queues it all in order:

```bash
npm run queue -- drain
```

With the scheduler installed (`npm run schedule -- install`), even that disappears — the
timer picks the spec up on its own.

Intake is tracked by file contents, one record per spec in `.agent-queue/planned/`: a spec is
never planned twice, and editing one re-plans it — replacing its tasks that have not run yet
rather than queueing a second set. Planning commits the spec, its tasks and its record
together (`Plan <spec>`), so a teammate who pulls sees it as planned too. A saved spec needs
no commit of its own first; the planning commit takes it.
`npm run queue -- plan` does the intake without draining, if you want to read the tasks
before anything runs.

Only top-level `specs/*.md` are planned: this README, `TEMPLATE.md` and anything in a
subfolder (a place for drafts) never are.

Working in a team (`project.workflow: "team"` in `agentic.config.json`)? Write the spec on a
branch of your own — the queue refuses to plan or build on the shared base branch — and let
its pull request carry the spec and everything built from it. `docs/AGENTIC_WORKFLOW.md` has
the whole arrangement.

`TEMPLATE.md` is a worked example. Copy it and edit.

## Files that are already built are history, not backlog

A spec still sitting here after a drain has already been planned; it is an archive of what
was asked for, not an open request. The durable answer to "what shipped" is `docs/builds/`.
If an old spec describes behaviour the code no longer has, believe the code and the build
records, not the stale sentence — and do not re-implement a finished brief.

## One file per feature

Name it for what it is — `product-archiving.md`, `invoice-export.md`. Do not split it into
tasks yourself; that is the planner's job, and it knows the repository's seams better than a
naming convention does. Write the feature as one document and let intake divide it.

## Build order is decided for you

Tasks are built **one at a time, each committing before the next starts**. So a task *can*
see everything built before it — that is the point of the ordering, not a caveat to work
around. What it cannot see is anything that has not run yet.

Each task can carry a `Depends-on:` line under its subject:

```markdown
Build the reporting dashboard
Depends-on: 01-reporting-data-contract
```

Each spec's tasks live in their own folder, `.agent-queue/todo/<spec>/`, so every spec numbers
from `01-` without clashing: a bare `Depends-on:` name means a task of the same spec, and
`<spec>/<name>` one from another spec. A task with unmet dependencies stays in `todo/`, is shown as
`[blocked: <task> (pending)]`, and is skipped by the drain. When the task it names lands —
its commit, naming it on a `Task:` line, is on the branch — the next drain picks it up on
its own. Nothing needs re-queueing by hand.

Two tasks that edit the same file are not independent even when their features are — give
one a `Depends-on:` on the other, so the later one builds on the earlier one's committed
result instead of overwriting it.

## What makes a brief work

The agent runs with nobody to ask, so ambiguity gets resolved by guessing. Three things
matter more than length:

1. **The first line is the commit subject, and the title of its build record.** Make it a
   sentence someone could read in a list of changes a year from now.
2. **Say what is out of scope.** "Do not touch the sign-in flow" prevents more wasted work
   than any amount of detail about what to build.
3. **Say how you will know it is done.** Verification is assumed; add the specific things
   you would check by hand.

Conventions are already in `CLAUDE.md`, and every brief tells the agent to read it — you do
not need to repeat them here.

## After a run

Verified work is **committed and left unpushed** (unless the operator set
`AGENT_AUTO_PUSH=1`). Review the accumulated commits and push when you are ready — pushing is
yours, and merging always is.

```bash
git log --oneline @{u}..HEAD           # what is built and waiting
git show <sha>                         # review one change
npm run auto:status                    # every run, and why any failed
```

Each task that lands writes its brief, with the files it changed, into its spec's directory
under `docs/builds/` (`<date-time>-<spec>/`, stamped when the spec was planned), in the task's
own commit. That directory is the durable answer to "why does this exist?" long after the
queue lane has been cleared.
