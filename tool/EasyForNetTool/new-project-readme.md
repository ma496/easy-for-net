# {{Name}}

An **ASP.NET 10 (FastEndpoints)** API in `src/backend` and a **Next.js 16** web app in
`src/frontend/web`, generated with [EasyForNet](https://easyfornet.com).
`CLAUDE.md` describes the architecture and `.claude/skills` holds a step-by-step guide for each recurring task.
The full documentation is at [easyfornet.com/docs](https://easyfornet.com/docs).

## Quick Start

You need the .NET SDK pinned in `global.json`, **Node.js 24+** with **pnpm 10** (`npm install -g pnpm`, or `corepack enable`), **Git** and **Docker** (or your own
PostgreSQL and Redis). The project's MCP servers for Claude Code (PostgreSQL, Redis, Playwright, in
`.mcp.json`) also need **[uv](https://docs.astral.sh/uv/)**; check them with `/mcp`.

```sh
# 1. Once: restore, build and add the initial migration
dotnet tool restore
dotnet build {{Name}}.slnx
dotnet ef migrations add Initial --project src/backend/Source
git add src/backend/Source/Migrations && git commit -m "Add the initial migration"

# 2. Run everything: PostgreSQL, Redis, the API and the web app
pnpm dev
```

Commit the migration: the task loop builds only from a clean tree, so an uncommitted one stops the
first `pnpm loop`.

Open **http://app.localhost:3000** and sign in as `admin` (the platform administrator: tenants and
editions), or **http://default.localhost:3000** as `tenantadmin` (the Default tenant's administrator).
Both use the password `Admin#123` locally. It is applied only when an account is first created, so
change it in the app afterwards. The API refuses to start on `Admin#123` outside Development and Testing.

Sign-up creates a tenant, so it is **off** until the platform administrator turns on **Allow sign-up**
under **Settings → General**.

## Development

`pnpm dev` starts PostgreSQL and Redis with Docker (`docker-compose.yml`) when they are not already
answering, installs the web app's packages when they are missing, and runs the API on :5000 (Swagger at
`/swagger`) and the web app on :3001 behind a dev proxy on :3000. The proxy sends `/api`, `/hubs`,
`/hangfire` and `/health` to the API and everything else to Next, as Caddy does in production. The web
app starts once the API answers on `/health`. **Ctrl+C stops everything**. The containers keep running
until `docker compose down`, which keeps the data.

```sh
pnpm dev --no-docker            # use the PostgreSQL and Redis you already run
pnpm dev --api-only             # services + API, no web app and no proxy
pnpm dev --web-only             # the web app and the proxy, against an API started elsewhere on :5000
pnpm stop:api                   # stop an API left running from this checkout (it locks bin/ on Windows)
pnpm stop                       # stop this checkout's whole running app (API, web app, dev proxy); Claude runs it after a backend edit
```

The containers publish on `DEV_POSTGRES_PORT` and `DEV_REDIS_PORT` from the root `.env`: 5432 and 6379,
or the next free ports if those were taken when the project was created. The connection strings in
`src/backend/Source/appsettings.Development.json` and `appsettings.Testing.json` (both git-ignored)
use the same ports, so change a port in both places. The development passwords are `postgres/postgres`
and `redis`. To use other ones, set `DEV_POSTGRES_PASSWORD` / `DEV_REDIS_PASSWORD` in the root `.env`
and update the connection strings to match. Development and Testing apply migrations on startup.

The web app runs on `DEV_PROXY_PORT` (3000, the address you open), with `next dev` behind it on
`DEV_WEB_PORT` (3001) and the API on `DEV_API_PORT` (5000), all in the root `.env` — or the next
free ports, if those were taken when the project was created. `pnpm dev` refuses a port that
something else already holds, says what holds it and suggests a free one; change the value in `.env`
and run it again. The API's `Web` origins follow `DEV_PROXY_PORT` without any other edit.

## Tests

```sh
dotnet test --project src/backend/Tests   # needs PostgreSQL (not Redis); uses its own `<database>_test` database
pnpm gate                                 # build, backend tests, web lint/typecheck/tests, next build
pnpm gate --fast                          # the same without the production web build
pnpm verify --autostart                   # only the gate steps your change reaches, plus a live API check
```

## Build With AI Agents

Describe a feature and the agents in `.claude/agents` build it the way the codebase expects:

```text
/feature add invoices with line items and PDF export
```

The lead loads the matching skills, hands design, data, backend and frontend work to specialists, runs
the gate, has QA, security and code reviewers sign off, and commits. Then it stops for your review.

For bigger work, write a spec in `specs/` (see `specs/TEMPLATE.md`) and let the loop build it unattended.
`/spec <what you want built>` drafts one with you in `specs/drafts/`, and moving it into `specs/` approves it:

```sh
pnpm loop                       # plan the spec into tasks, build each one, verify, review, commit
pnpm monitor                    # watch the run live on http://127.0.0.1:4000
pnpm auto "<task>"              # build one task now, outside the queue
```

The runner verifies every task itself, caps what each one costs, and commits one task at a time. It
never pushes or merges unless you tell it to. `specs/README.md` and `docs/AGENTIC_WORKFLOW.md` explain
the loop, and `agentic.config.json` configures it.

## Production (Docker)

`docker-compose.prod.yml` runs the API, the web app, PostgreSQL, Redis and Caddy (HTTPS). The platform is
served at `app.<DOMAIN>` (or `<PLATFORM_SUBDOMAIN>.<DOMAIN>`) and every tenant at `<identifier>.<DOMAIN>`.
Its values come from the root `.env`, which was generated with random passwords, a JWT signing key, a
random `SEED_ADMIN_PASSWORD` for the administrators and `DOMAIN=localhost`. It is git-ignored, so back it
up. `.env.docker.example` documents every value.

```sh
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```

Before deploying for real:

- point two DNS A records at the server: the platform's host (`app.<DOMAIN>` by default) and `*.<DOMAIN>`;
- set `DOMAIN`, `PLATFORM_SUBDOMAIN`, `PUBLIC_URL` (`https://<PLATFORM_SUBDOMAIN>.<DOMAIN>`) and
  `TENANT_URL_TEMPLATE` (`https://{tenant}.<DOMAIN>`);
- fill in the SMTP values.

Caddy obtains an HTTP-01 certificate per host on demand: the platform's at startup, and each tenant's on
its first visit once the API confirms the tenant exists. No DNS credentials are needed. Let's Encrypt
issues at most 50 certificates per domain per week, so an installation expecting more new tenants than
that should use a DNS-01 wildcard certificate (see `docker/Caddyfile`).

## Deploy to a VPS (Coolify)

`pnpm deploy:vps` puts the application on a Linux VPS behind [Coolify](https://coolify.io)
from Windows, macOS or Linux. It needs Node and an OpenSSH client on your machine. On the server it needs
SSH access as root (or a sudo user) and the same two DNS A records. `--domain` is the domain the
application lives under, not the platform's host.

```sh
pnpm deploy:vps --host 203.0.113.10 --domain example.com --email you@example.com
pnpm deploy:vps --domain example.com                        # later deploys remember the other answers
EFN_DNS_TOKEN=<token> pnpm deploy:vps --domain example.com --dns-provider cloudflare         # *.example.com wildcard certificate
pnpm deploy:vps --domain example.com --platform-subdomain demo         # the platform at demo.example.com
pnpm deploy:vps --host 203.0.113.10 --check                 # only report the server's prerequisites
pnpm deploy:vps --help                                      # every option
```

It checks the server and stops if anything would fail, then installs only what is missing (Coolify
included), creates a Coolify application for the domain, fills in its environment and deploys.
Running it again only redeploys: the passwords and JWT key are generated once on the server and never
overwritten. Coolify builds from the Git repository with `docker-compose.coolify.yml`, so it deploys
what is **pushed** to the branch.

Coolify's Traefik cannot issue a certificate per tenant host on demand. Pass `--dns-provider` with an
API token in `EFN_DNS_TOKEN` to get one `*.<domain>` wildcard certificate through DNS-01. For a domain
on GoDaddy, `--godaddy-key` and `--godaddy-secret` also create the `*.<domain>` record for you. Without
DNS-01, tenant hosts get Traefik's default certificate, so use `docker-compose.prod.yml` instead. The
Coolify dashboard is at `http://<server>:8000`. Its login and the app's administrator password are saved
on the server in `/root/.efn-deploy/`.
