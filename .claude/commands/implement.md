---
description: Execute a spec's tasks.md in file-disjoint waves, with review and build gates (stage 3 of 4)
argument-hint: [NNN-slug] [--only T-003,T-004]
---

Stage 3 of the spec-driven loop. Read `.claude/skills/spec-driven/SKILL.md` first if you have not
already.

Arguments: **$ARGUMENTS**

Resolve the spec directory as in `/plan`: use the one named in the arguments, otherwise the
highest-numbered directory under `specs/` that has a `tasks.md` with unticked boxes. Ask rather than
guess when it is ambiguous.

Before running, check the working tree is clean enough that the user will be able to read the diff —
if there are unrelated uncommitted changes, say so and let them decide.

Run the `spec-implement` workflow with `args` `{"specDir": "specs/<NNN-slug>"}`, adding
`"tasks": ["T-003", "T-004"]` when the user passed `--only`. This is an explicit user opt-in to
multi-agent orchestration — invoke the Workflow tool with `name: "spec-implement"`, do not write a
script.

When it returns:

1. Report waves run, tasks completed, and the gate result — including any command the gate recorded
   as skipped, such as the backend tests when no database is running. A skipped gate is not a pass.
2. List `failed` and `unscheduled` tasks with their reasons. `unscheduled` means the scheduler could
   not place them, usually a dependency cycle or a dependency on a task id that does not exist — fix
   `tasks.md` and re-run rather than implementing them by hand.
3. Surface `followUps`, especially any entry saying an agent wanted to edit a file its task did not
   declare — that is a sign `tasks.md` under-declared a file set.
4. Show the user `git diff --stat` so they can review, and say that `/verify <NNN-slug>` is next.

Do not commit anything unless the user asks.
