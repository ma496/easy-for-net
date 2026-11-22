---
description: Draft a spec with the user — ask what matters, write it to specs/drafts/ for review, never queue it
argument-hint: "<what you want built> | <specs/drafts/name.md> <what to change>"
allowed-tools: Bash, PowerShell, Read, Write, Edit, Grep, Glob, AskUserQuestion
---

Draft a spec: **$ARGUMENTS**

Write **no implementation code** and **nothing in top-level `specs/`**. A file there is
planned and built by the next drain, so your only output is a draft in `specs/drafts/`, which
is never planned. The user approves the draft by moving it into `specs/` themselves.

If the arguments name an existing `specs/drafts/*.md`, revise that draft as asked. Do not
start a new one: read it, apply the change, and ask questions only if the change raises new
ones.

## 1. Read before asking

Read `specs/README.md` and `specs/TEMPLATE.md`: the template is the shape, the README what makes
a brief work. Then find where the feature belongs (the slice, its entities, permissions and
screens, a similar feature to follow) so the spec names real places. Anything the code
answers is not a question for the user.

Check `specs/` and `docs/builds/` for a spec that already covers this. If one does, say so and
ask whether this is a change to it or new work.

## 2. Ask only what changes the work

The spec goes to an agent with nobody to ask, so every ambiguity left in it gets guessed.
Resolve the ones that matter now, in **one** `AskUserQuestion` call of at most four
questions, each giving concrete options and recommending one. Ask only when two answers would
lead to different work, for example:

- who may do it (which permission, and whether it is tenant or platform scope)
- whether it depends on the tenant's plan (a feature or a limit)
- what is out of scope
- what a person would check by hand to call it done

When the request is already clear, ask nothing and say so. Do not ask a second round unless an
answer opened something you could not have foreseen.

## 3. Write the draft

Write `specs/drafts/<name>.md`, named for what it is (`product-archiving.md`), in
`TEMPLATE.md`'s shape:

- **First line**: the commit subject, a sentence that still reads well in a change log a
  year from now.
- A short paragraph: what and why.
- `## Scope`: concrete, checkable bullets naming the real entities, endpoints, permissions
  and screens.
- `## Out of scope — do not touch`: the neighbouring areas this must leave alone.
- `## Done when`: `pnpm verify` passes, plus the tests and hand checks that prove it.
- `## Open questions`: only if something is still undecided, with the assumption the draft
  makes for each. Leave it out when there is nothing open.

One file per feature. Do not split it into tasks, since that is the planner's job. Do not
repeat conventions `CLAUDE.md` already states. Do not commit the draft.

## 4. Hand it over, then stop

Report the draft's path, the assumptions you made, and the open questions. Then tell the
user:

- To review it, edit it themselves or run `/spec specs/drafts/<name>.md <what to change>`.
- To approve it, resolve or delete `## Open questions` first, then move the file out of
  `specs/drafts/` into `specs/`, by dragging it in the editor or with the exact command
  written out with the draft's real name, e.g.
  `mv specs/drafts/product-archiving.md specs/product-archiving.md`. Only files directly in
  `specs/` are planned, and the next `pnpm queue plan` or drain picks it up.
- Finish editing before moving it: editing a spec in `specs/` plans it again and replaces its
  tasks that have not run yet.

Moving the draft into `specs/` is the user's approval, so leave it to them: do not move it,
and do not run `pnpm queue plan` or `pnpm queue drain`, which would start
building it.
