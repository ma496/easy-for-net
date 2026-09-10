---
description: Trace every acceptance criterion to code and test evidence, and emit the remaining work (stage 4 of 4)
argument-hint: [NNN-slug]
---

Stage 4 of the spec-driven loop, and its convergence gate. Read
`.claude/skills/spec-driven/SKILL.md` first if you have not already.

Arguments: **$ARGUMENTS**

Resolve the spec directory as in `/plan`: use the one named in the arguments, otherwise the
highest-numbered directory under `specs/` that has an `implementation.md`. Ask rather than guess.

Run the `spec-verify` workflow with `args` `{"specDir": "specs/<NNN-slug>"}`. This is an explicit
user opt-in to multi-agent orchestration — invoke the Workflow tool with `name: "spec-verify"`, do
not write a script.

When it returns:

1. Report the counts: satisfied, partial, missing. Every criterion is traced, so `acsVerified` below
   `acsTotal` means a tracer died rather than that the run was capped — say so plainly, and note that
   the workflow refuses to report convergence in that case.
2. List every `partial` and `missing` criterion with its gap. A criterion downgraded from satisfied
   to partial was refuted by independent review; that is a real finding, not a formality.
3. Surface `specDrift` separately: code that no criterion asked for. Each entry is a decision for the
   user — remove the code, or amend the spec.
4. If `converged` is false, tell the user the new task ids and offer to run
   `/implement <NNN-slug> --only <ids>` followed by `/verify` again. Repeat until it converges.
5. If `converged` is true, say so, point at `verification.md`, and stop.

Never edit `spec.md` to make a criterion pass. If a criterion turned out to be wrong, say so and let
the user decide to change it.
