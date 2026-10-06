/**
 * The brief the planner is given when it turns a spec into queued task files.
 *
 * Planning is the one phase with no code to check it: a badly split spec produces tasks
 * that each pass their own gate and still leave the product broken, and nothing downstream
 * can tell. So the instruction is precise about the seam — what belongs in one task, what
 * belongs in two, and how a task declares what it must wait for.
 *
 * Like the build brief, the shape is universal and only the names come from
 * `agentic.config.json`.
 */
import { config, PROJECT_NAME } from "./project-config.mjs";
import { nextNumber } from "./planning.mjs";

/**
 * `scope` is the spec's folder (lib/task-names.mjs): every task file is written inside it.
 * `taken` the names an earlier plan of the same spec already used there, which must not be
 * reused — numbering carries on after the highest.
 */
export function buildPlanBrief({ scope = "plan", taken = [] } = {}) {
  const earlier = taken.length
    ? `\n\nThis spec was planned before, and its folder already uses these names: ${taken.join(", ")}.\n` +
      `Do not reuse any of them; number the new tasks from ${nextNumber(taken)}. A new task may depend\n` +
      "on one of those by name."
    : "";
  const guide = config.docs.guide ?? "CLAUDE.md";
  const capabilities = config.docs.capabilities;

  return `You are planning work in the ${PROJECT_NAME} repository. Read ${guide}
first. Write NO implementation code — your entire output is task files.

Split the spec below into task files under .agent-queue/todo/${scope}/. A task file becomes one
commit, worked by an agent that reads ONLY that file.

Find the seams. The unit is the smallest thing worth reviewing and shipping on its own,
not the smallest thing that could be written separately. Keep together anything that would
leave the build or the product broken if only half shipped — a schema column and the query
that reads it, a helper and its test. Split apart work that touches different areas and
could ship in either order. If the spec is genuinely one feature, write one task; do not
manufacture splits.

Every task pays a fixed cost before its own work counts — a verification run and a round of
reviews — so a task too small to be worth that is part of its neighbour. In particular,
documentation belongs to the task whose change it describes: that task updates the guide,
the skills and the docs its change makes out of date. Do not end a batch with a task that
only writes down what the others built, unless the spec asks for documentation alone.

Order matters. Tasks are built one at a time and each is committed before the next starts,
so a task CAN see the code of any task that ran before it — and none of the code of a task
that has not run yet. Every task that needs another task's code must declare it on a
"Depends-on:" line; the queue holds it until that task is done. Two tasks that need each
other both ways are one task — merge them.

Write each file as .agent-queue/todo/${scope}/NN-short-slug.md — in exactly that folder, a
two-digit number in dependency order, then a lowercase slug.${earlier}

Use exactly this shape:

<one line: what this does — it becomes the commit subject>
Depends-on: 01-earlier-task, 02-other-task     (omit this line entirely if nothing blocks it)

<a short paragraph of what to build and why>

## Scope
- concrete, checkable bullets

## Out of scope — do not touch
- the neighbouring areas this task must leave alone, INCLUDING any file another task in
  this same batch owns — name those files explicitly so a later task does not undo an
  earlier one

## Done when
- \`${config.commands.verify}\` passes
- the specific things a person would check by hand

If the spec is already written as a single self-contained task brief, do not re-split it:
queue it as one file, written the same way, carrying over any dependency it names. A
dependency names a task of this plan by its file name without .md; a task from another plan
is named <its folder>/<its file name without .md>.
${
  capabilities
    ? `
Capability records: for any area the spec touches, check ${capabilities}/ for a matching
record and read it. Carry its observable behaviours into each task's Done when section that
could affect them — a change that satisfies its brief and breaks a standing promise must be
caught before it ships.
`
    : ""
}
Repo conventions are already in ${guide}, which every run is told to read. Do not repeat
them.

THE SPEC FOLLOWS:

`;
}
