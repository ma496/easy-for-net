---
description: Build a new feature end to end — plan, implement, verify, review, commit, then stop for approval
argument-hint: "<what you want built>"
allowed-tools: Bash, Read, Edit, Write, Grep, Glob, Agent
---

Build this feature: **$ARGUMENTS**

Work through the stages below in order. Do not skip a stage silently — if you skip one, say
why in your final report.

## 1. Scope

Restate the feature in one or two sentences, including what is explicitly *out* of scope. If
two readings of the request would lead to materially different work, ask now — before
writing code. Otherwise state your assumption and continue.

## 2. Locate

Find where this belongs before writing anything. Report the files you expect to touch and
which layer each belongs to. The pattern you need usually already exists — reuse beats
invention.

## 3. Design, if a person will look at it

If this feature has a screen, delegate to `ui-ux-reviewer` **first** and build from the
brief it returns. A design decided after the build is a rebuild.

## 4. Implement

Delegate to the specialist that owns each area — the `departments` map in
`agentic.config.json` says which. Use several in parallel only when the pieces are genuinely
independent; a schema change and the queries reading it are not.

Honour the rules in `CLAUDE.md` and `project.conventions`.

## 5. Verify

```bash
npm run verify -- --autostart
```

If the change alters behaviour a person would notice, exercise it for real and quote the
actual output. A passing typecheck alone does not prove behaviour.

## 6. Review

Dispatch the first review tier together, then `code-reviewer` last. Fix CHANGES NEEDED
before committing.

## 7. Commit, then stop

Commit — deliberately, staging only the paths this feature touched:

```bash
git add <the specific files>        # never `git add -A`
git commit
```

Then **stop**. Pushing needs the owner's approval. Never merge a pull request.

## Final report

State: what you built, which gates passed and which were skipped, what you verified by hand,
what you did **not** verify, and the commit.
