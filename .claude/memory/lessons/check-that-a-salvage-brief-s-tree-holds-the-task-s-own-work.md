---
scope: always
learned: 2026-09-29
task: 06-document-session-validation-and-revocation
---

# Check that a salvage brief's tree holds the task's own work

A SALVAGE brief can arrive when the only uncommitted changes are runner bookkeeping (.agent-queue moves, docs/builds records), so verify passes on nothing. Diff the task's own paths before choosing review-only; if they are untouched, build the task in full.
