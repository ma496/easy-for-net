---
description: Run a task to completion with no human in the loop — implement, verify, review, commit, then stop
argument-hint: "<what you want done>"
allowed-tools: Bash, Read, Edit, Write, Grep, Glob, Agent
---

Run this task unattended: **$ARGUMENTS**

Nobody is going to answer a question. Do not ask one. Where the request is ambiguous, make
the reasonable call, state the assumption in your final report, and finish the work.

This is the same loop `npm run auto -- "<task>"` drives from the shell. Use that command
when you want it fully detached; use this when you are already in a session.

## 1. Work in place

Build here, in this checkout, on the project's working branch. Do not create a branch and do
not use a worktree — a task that puts its work anywhere else puts it where the next task
cannot see it.

Start from a clean tree. If there are uncommitted changes that are not yours, stop and say
so rather than folding somebody else's work into this task's commit. If a drain is running
(the status line shows `drain●`), stop — do not edit the same tree in parallel.

## 2. Implement, through the agent that owns the area

Locate the existing pattern before writing anything — grep, or `/trace`. Then delegate; do
not do every layer yourself. `departments` in `agentic.config.json` is the map of which
agent owns which paths, and it is the same map the runner checks you against.

Load the skill your work owes rather than reasoning from scratch; `skills` in the same file
says which change owes which procedure.

## 3. Verify — the part that is not optional

```bash
npm run verify -- --autostart
```

It reads the diff and runs what that diff demands. **A required check that could not run
counts as a failure** — that is the point of it.

If it fails, fix the cause and run it again. **Do not** make it pass by widening a type,
adding a suppression comment, deleting an assertion, or weakening a test.

Add unit tests for any pure helper you add or change.

## 4. Review — also not optional

After the writers finish, dispatch the first review tier together, then `code-reviewer`
last, once the others have reported.

`npm run auto` refuses an attempt that skipped a department owed by the finished diff. Hold
yourself to the same bar here.

## 5. Commit, then stop

Commit — deliberately, staging only the paths this task touched, never `git add -A`.
Committing is routine here: it is what lets the next task build on this one.

Then **stop**. Do not push unless the owner has set `AGENT_AUTO_PUSH=1` for the runner. Do
not merge a pull request — hooks block every merge route.

## 6. Report

State: what you built, the assumptions you made without asking, the gate results, what you
verified by hand, **what you did not verify**, and the commit.
