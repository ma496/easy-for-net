---
description: Write a commit message for the current changes — never commits
argument-hint: "[staged | <paths> | <hint about the change>]"
allowed-tools: Bash(git status:*), Bash(git diff:*), Bash(git log:*), Read, Grep, Glob
---

Write a commit message for the changes in this working tree. **Do not commit, stage, unstage
or otherwise change the tree** — the output is the message, nothing else.

Arguments: **$ARGUMENTS**

## 1. Pick the scope

- No arguments: the staged changes if anything is staged, else every uncommitted change
  (tracked edits and untracked files).
- `staged`: the staged changes only.
- Paths: only those files or directories.
- Anything else is a hint from the user — the intent, a ticket, what to emphasise. Use it to
  frame the message, but describe only what the diff actually does.

## 2. Read the change

`git status`, then `git diff --cached` / `git diff` for the scope, and read untracked files in
it. Read surrounding code where the diff alone does not say *why*. `git log -10 --format='%s%n%b---'`
shows this repository's message style — match it.

## 3. Write it

- **Subject:** one line, imperative or a plain statement of the outcome, under ~72
  characters, no trailing period unless the log uses one, no type prefix unless the log does.
- **Body** (skip it for a trivial change): why, then what — a short paragraph or `-` bullets,
  wrapped at 72. Name files only when it helps. Mention tests added or changed.
- No `Co-Authored-By` or any other attribution trailer.
- If the scope mixes unrelated changes, say so and offer one message per group instead of
  one message that hides it.

## 4. Output

The message in a single fenced code block, ready to paste, followed by one line naming the
scope it covers (e.g. "Covers the 4 staged files"). Nothing is committed.
