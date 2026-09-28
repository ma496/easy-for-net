---
description: Run the static gate — build, backend and web tests, lint, typecheck, hook tests, web build
allowed-tools: Bash, Read
---

Run `npm run gate` (`commands.gate` in `agentic.config.json`). `npm run gate -- --fast`
skips the production web build; `npm run gate -- --only api|web|engine` runs one area.

It is the static half of verification: it proves the solution builds, the backend tests pass
(they need PostgreSQL running), the web app lints, typechecks and passes vitest, the hooks
still behave as documented, and the web app builds. It proves nothing about
behaviour — for that, run `/verify`, which adds the live checks the diff demands.

If it fails, fix the failure before anything else. Report exactly what failed, not a summary.
