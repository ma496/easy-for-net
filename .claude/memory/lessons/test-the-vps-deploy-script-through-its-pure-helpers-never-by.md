---
scope: deploy
learned: 2026-10-17
---

# Test the VPS deploy script through its pure helpers, never by running it

The shell guard refuses any command line that names the deploy entry script, even with --help. Put new logic in the lib helper beside it and cover it in its node test, exercise remote.sh functions by sourcing them into a scratch shell with docker stubbed, and check the Caddyfile with caddy validate in the caddy:2-alpine image.
