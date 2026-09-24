---
description: Fix a bug end to end — reproduce, locate, fix, prove the repro now passes
argument-hint: "<what is broken>"
allowed-tools: Bash, Read, Edit, Write, Grep, Glob, Agent
---

Fix this: **$ARGUMENTS**

## 1. Reproduce first

Do not read code yet. Make the bug happen, and write down the exact command, input, or
click that does it, plus what you saw. If you cannot reproduce it, say so and stop — a fix
for a bug nobody has seen is a guess, and it will be reviewed as one.

Where the project has a procedure for this class of problem, load that skill now rather than
reasoning from first principles. The pipeline's early exits are the usual answer, and they
look exactly like the bug you were about to fix somewhere else.

## 2. Locate

Follow the path from the symptom backwards. Name the file and line where the wrong thing is
decided, not the file where it becomes visible. Those are usually different.

## 3. Fix the cause

Fix where the decision goes wrong. A guard added at the surface to hide a wrong value is not
a fix; it is a second bug that makes the first one harder to find.

## 4. Prove it

Run the reproduction from step 1 again and quote the new output. Then run `npm run verify`.
Add a unit test that fails on the old behaviour if the logic is testable without I/O.

## 5. Report

State: the reproduction, the cause in one sentence, the fix, the proof it now passes, and
anything nearby that is probably wrong for the same reason but was out of scope.
