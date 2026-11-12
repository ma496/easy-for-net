---
description: Review the working-tree diff against this repo's conventions
allowed-tools: Bash, Read, Grep, Glob
---

Review the uncommitted diff (`git diff` and `git diff --staged`, plus untracked files).

Read `CLAUDE.md`, the nested guide nearest the changed files, and `project.conventions` in
`agentic.config.json` first — those are the rules this repository actually holds itself to,
and they are the ones a generic review misses.

Beyond ordinary correctness, check:

- **Isolation** — a caller's own data is read and written by the session's account, never by
  an id from the request, and no feature reaches into another feature's entities.
- **Authorization** — every new endpoint declares its permission or is deliberately anonymous
  (anyone may sign up, so "signed in" is not "administrator"), a new permission exists on both
  the API and the web side, and every new admin screen is in `auth-urls.ts` behind
  `Admin.Access`. An anonymous endpoint returns only what a public page would show.
- **The conventions a compiler cannot catch** — the ones listed in the config, by hand.
- **Weakened checks** — a loosened type, a deleted assertion, a narrowed test, a widened
  guard pattern. This is the failure mode that makes an unattended loop untrustworthy.
- **Secrets** — no credentials from the per-environment settings or env files inlined, no
  keys in committed files.
- **Blast radius** — anything that reaches another visitor's data, hands an admin capability
  to an ordinary account, or serves one visitor's server-rendered answer to another through a
  cached fetch.
- **Build output** — no edits to generated directories.

Report findings most severe first with `file:line`. Say explicitly if the diff is clean.
