---
description: Orient at the start of a session — tree, queue, in-flight work, and what to do next
allowed-tools: Bash, Read, Grep, Glob
---

Work out the current state of this project and report it, then propose the next step.

1. **Working tree.** `git status --porcelain` and `git log --oneline -5`. Work happens on
   the project's working branch, so being on it is normal. Flag a dirty tree explicitly: a
   drain refuses to start while anything outside `.agent-queue/` is uncommitted.
2. **In-flight work.** If files are modified, read the diff and describe in one or two
   sentences what the change in progress appears to be, and whether it looks finished.
3. **Divergence.** How many commits is the working branch ahead of `origin`? Those are
   verified and waiting for the owner to push.
4. **The loop.** `npm run queue` for what is waiting or blocked, `npm run schedule --
   status` for whether the timer is running, and `npm run lessons` for what past runs
   recorded. Check for an in-flight drain before suggesting any edit — a task mid-run
   commits the tree it finds.

Then give a short recommendation: the single most useful next action.

Keep the whole report under twenty lines. This is orientation, not an audit.
