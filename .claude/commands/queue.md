---
description: Manage and drain the autonomous task queue — add work, see what is waiting, run it down
argument-hint: "[add <task> | list | drain | resume | plan]"
allowed-tools: Bash, Read, Grep, Glob
---

The queue is how work reaches the loop without anyone typing a command per task. A task is
a markdown file in `.agent-queue/todo/`; anything that can write a file can create work.

Interpret `$ARGUMENTS` and run the matching command from the repo root:

| Intent | Command |
|--------|---------|
| nothing, or "list"/"status" | `npm run queue` |
| "add ..." | `npm run queue -- add "<the rest of the arguments>"` |
| "drain" | `npm run queue -- drain` |
| "resume" | `npm run queue -- resume` — requeue what an interrupted run left in `doing/` |
| "plan" | `npm run queue -- plan` — turn new `specs/*.md` into tasks without building |

## Draining

Tasks are built **one at a time, in this checkout**, each committing before the next starts
— which is what lets a task build on the one before it. `--parallel` is refused rather than
quietly ignored, because two tasks sharing a working tree would overwrite each other.

The ready set is recomputed after every task, so a task unblocked by the one that just
landed runs immediately rather than waiting for the next scheduled firing.

Every task is routed to the agents that own the paths in its finished diff, and an attempt
that skipped one is refused however green its gate.

Report per task: whether it verified, and the commit it produced. Then stop. **Do not push
or merge** — verified work accumulates as local commits for the owner to review and push.

A drain refuses to start while anything outside `.agent-queue/` is uncommitted. It spends
real credit and spawns agents, so confirm before starting one if the user did not clearly
ask for it in this turn.

## After

`npm run auto:status` shows the run history, including why a failed task failed.
`npm run queue -- resume` requeues anything an interrupted run left in `doing/`.
`npm run lessons` shows what past runs recorded for future ones to read.
