---
description: Turn a spec into plan.md, contract documents and a coverage-gated tasks.md (stage 2 of 4)
argument-hint: [NNN-slug] [--focus "steer the design"]
---

Stage 2 of the spec-driven loop. Read `.claude/skills/spec-driven/SKILL.md` first if you have not
already.

Arguments: **$ARGUMENTS**

Resolve the spec directory before doing anything else. If the arguments name one, use
`specs/<that>`. If they do not, list `specs/` and pick the highest-numbered directory that has a
`spec.md` but no `tasks.md`; when that is ambiguous, ask the user which one rather than guessing.

Open `spec.md` and check `## Open questions`. If anything there is still unanswered, resolve it with
the user first — the workflow will refuse the run otherwise.

Run the `spec-plan` workflow with `args` `{"specDir": "specs/<NNN-slug>"}`, adding `"focus"` when the
user passed `--focus`. This is an explicit user opt-in to multi-agent orchestration — invoke the
Workflow tool with `name: "spec-plan"`, do not write a script.

When it returns:

1. Report the chosen approach, the scores of the alternatives it beat, and the task count.
2. If `coverageClean` is false, list `uncoveredAcs` and `orphanTasks` plainly — the plan is not ready
   to implement until every criterion maps to a task.
3. If `needsClarification` is non-empty, put those questions to the user with `AskUserQuestion` and
   fold the answers into the relevant contract document.
4. Point the user at `plan.md` and `tasks.md` to review, and say that `/implement <NNN-slug>` is next.

Do not write application code in this stage.
