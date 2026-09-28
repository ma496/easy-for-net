---
description: Split a multi-feature spec into one queued task per feature — no code written
argument-hint: "<path to the spec file>"
allowed-tools: Bash, Read, Write, Grep, Glob
---

Split this spec into queued tasks: **$ARGUMENTS**

Write **no implementation code**. Your entire output is task files under
`.agent-queue/todo/`, plus a short report.

## Find the seams

The unit is the smallest thing worth reviewing and shipping on its own — not the smallest
thing that could be written separately.

- Keep together anything that would leave the build or the product broken if only half
  shipped: a schema column and the query that reads it, a helper and its test.
- Split apart work that touches different areas and could ship in either order.
- If the spec is genuinely one feature, write one task. Do not manufacture splits.

## Order is enforced, not suggested

Tasks are built one at a time and each commits before the next starts, so a task can see the
code of every task that ran before it and none of a task that has not run yet. Declare every
dependency on a `Depends-on:` line. Two tasks that need each other both ways are one task.

## The shape of each file

Name each file `.agent-queue/todo/NN-short-slug.md`, numbered in dependency order:

```markdown
<one line: what this does — it becomes the commit subject>
Depends-on: 01-earlier-task          (omit the line entirely if nothing blocks it)

<a short paragraph of what to build and why>

## Scope
- concrete, checkable bullets

## Out of scope — do not touch
- the neighbouring areas this task must leave alone, including any file another task in
  this same batch owns — name those files explicitly

## Done when
- `npm run verify` passes
- the specific things a person would check by hand
```

## Report

List the files you wrote, the dependency order, and anything in the spec you deliberately
left unqueued and why.
