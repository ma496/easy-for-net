---
description: Verify the current change — the static gate plus whatever live checks the diff demands
argument-hint: "[--scope working] [--autostart]"
allowed-tools: Bash, Read, Grep, Glob
---

Run `npm run verify -- $ARGUMENTS` from the repo root (the `--` hands the flags to the
script rather than to npm).

It reads the diff and decides what this change actually needs: the static gate always, plus
every check in `verify.checks` whose watched paths the diff touched. `agentic.config.json`
is where that mapping lives — read it if you want to know why a check did or did not run.

Useful flags: `--autostart` starts the app if a live check needs it, `--scope working`
judges only uncommitted changes instead of the whole branch.

## Reading the result

**A required check that could not run is a failure, not a footnote.** If it reports that
nothing was serving, the change is *unverified* — do not describe it as passing. Either
start the app and re-run, or say plainly which check did not happen.

Report which checks ran and which were skipped; the script prints both. Then stop —
committing and pushing are the owner's to approve, and merging is theirs alone.
