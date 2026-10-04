---
description: Write a pull-request title and description in markdown — never opens a PR
argument-hint: "[<base branch> | <hint about the change>]"
allowed-tools: Bash(git status:*), Bash(git diff:*), Bash(git log:*), Bash(git branch:*), Bash(git rev-parse:*), Bash(git symbolic-ref:*), Bash(git merge-base:*), Read, Grep, Glob
---

Write a pull-request title and description for the current branch. **Do not open, edit or
comment on a pull request, and do not push, commit, stage or otherwise change the tree** —
the output is the text, nothing else.

Arguments: **$ARGUMENTS**

## 1. Pick the base

- An argument naming an existing branch: that branch.
- Otherwise `project.baseBranch` in `agentic.config.json`, else `origin/HEAD`
  (`git symbolic-ref refs/remotes/origin/HEAD`), else `master` / `main`, whichever exists.
- Anything else in the arguments is a hint from the user — the intent, a ticket, what to
  emphasise. Use it to frame the text, but describe only what the branch actually does.

## 2. Read the change

`git log --format='%s%n%b---' <base>..HEAD` for every commit on the branch, and
`git diff --stat <base>...HEAD` then `git diff <base>...HEAD` for what it changes. Read
surrounding code where the diff alone does not say *why*. If the working tree has uncommitted
changes, say so in one line after the output — they are not part of the PR.

## 3. Write it

- **Title:** one line, under ~70 characters, a plain statement of the outcome, no trailing
  period, no type prefix unless the branch's commits use one.
- **Description**, in markdown:
  - `## Summary` — why the change exists and what it does, a short paragraph or 2–5 bullets.
  - `## Changes` — `-` bullets grouped by area (API, web, tooling, docs…), naming files or
    components only where it helps a reviewer find them.
  - `## Testing` — tests added or changed, and how it was verified (e.g. `npm run verify`).
    Claim only what the commits or the tree show; if verification is unknown, say what to run.
  - `## Notes` — only when there is something a reviewer must know: migrations, configuration,
    breaking changes, follow-ups. Omit the section otherwise.
- No attribution line, `Co-Authored-By` trailer or "Generated with" footer.
- If the branch mixes unrelated changes, say so and suggest how it could be split.

## 4. Output

The title on its own line as `# <title>`, then the description, all inside a single fenced
` ```markdown ` block ready to paste, followed by one line naming what it covers
(e.g. "Covers 3 commits on `feat/x` against `master`"). No pull request is created.
