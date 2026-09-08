---
description: Write a spec with EARS acceptance criteria for a feature request (stage 1 of 4)
argument-hint: <one-line feature request>
---

Stage 1 of the spec-driven loop. Read `.claude/skills/spec-driven/SKILL.md` first if you have not
already — it holds the document template, the EARS grammar and the completeness checklist.

The request is: **$ARGUMENTS**

If the request is empty, ask what should be specified and stop.

Run the `spec-specify` workflow with `args` `{"request": "<the request above>"}`. This is an explicit
user opt-in to multi-agent orchestration — invoke the Workflow tool with `name: "spec-specify"`, do
not write a script.

When it returns:

1. Tell the user the spec directory and how many acceptance criteria were written.
2. If `blockingQuestions` is non-empty, put them to the user with `AskUserQuestion`, one question per
   entry, offering the workflow's `defaultAssumption` as the first option where there is one. Then
   edit `spec.md` yourself: fold each answer into the relevant criterion or into `## Assumptions`, and
   remove the question from `## Open questions`. `spec-plan` refuses to run while that section still
   has unanswered entries.
3. Summarise the criteria in a few lines so the user can sanity-check the scope without opening the
   file, and name anything listed under `residualGaps`.
4. Say that `/plan <NNN-slug>` is next.

Do not start designing or writing code — this stage produces a specification and nothing else.
