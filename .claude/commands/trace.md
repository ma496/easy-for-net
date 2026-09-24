---
description: Trace how a request or feature flows through the codebase
argument-hint: "<the thing to follow>"
allowed-tools: Bash, Read, Grep, Glob
---

Follow this through the codebase: **$ARGUMENTS**

Start at the entry point — the route, the event handler, the command — and follow the call
path to where the work is actually done and back out to what the caller receives.

Report it as an ordered list of `file:line` steps, each with one sentence on what happens
there. Name the early exits: every place the path can return before reaching the end is
usually the answer to "why did nothing happen?".

Finish with the two or three files someone changing this behaviour would need to touch.
