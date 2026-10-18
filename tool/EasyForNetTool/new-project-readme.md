# {{Name}}

An ASP.NET (FastEndpoints) API in `src/backend` and a Next.js web app in `src/frontend/web`, generated
with `dotnet efn`. `CLAUDE.md` describes the architecture; `.claude/skills` holds step-by-step guides.

## Prerequisites

- The .NET SDK pinned in `global.json`
- Node >= 24
- Docker (for PostgreSQL and Redis in development), or your own PostgreSQL on `localhost:5432` and
  Redis on `localhost:6379`

The development containers are published on the ports `DEV_POSTGRES_PORT` and `DEV_REDIS_PORT` in
the root `.env`. When the project was created, 5432 and 6379 were used unless something on the
machine already held them, in which case the next free ports were picked and written into the
connection strings in `src/backend/Source/appsettings.Development.json` and `appsettings.Testing.json`
as well. To change a port later, change it in both places.

## Development

Once, from the root — `dotnet ef` needs the packages restored, and the API needs a migration to create
its database:

```sh
dotnet tool restore                                       # dotnet-ef
dotnet build {{Name}}.slnx
dotnet ef migrations add Initial --project src/backend/Source
git add src/backend/Source/Migrations && git commit -m "Add the initial migration"
```

The commit matters: the task loop builds only from a clean tree, so an uncommitted migration
stops the first `npm run loop`.

Then, from the root, run the whole application:

```sh
npm run dev
```

It starts PostgreSQL and Redis with Docker (`docker-compose.yml`) when they are not already answering,
installs the web app's packages when they are missing, and runs the API (http://localhost:5000, Swagger
at `/swagger`) and the web app together in one terminal. The web app (`next dev` on :3001) sits behind a
dev proxy on :3000 (`scripts/dev-proxy.mjs`) that, like Caddy in production, sends `/api`, `/hubs`,
`/hangfire` and `/health` to the API and everything else to Next, so the browser reaches the API on its
own origin. Open http://app.localhost:3000 or http://default.localhost:3000 (http://localhost:3000 works
too). The web app starts only once the API answers on `/health`, so its first page never meets an API
still building. **Ctrl+C stops everything**; the containers keep running until `docker compose down` (which
keeps the data).

```sh
npm run dev -- --no-docker      # use the PostgreSQL and Redis you already run
npm run dev -- --api-only       # services + API, no web app and no proxy
npm run dev -- --web-only       # the web app and the proxy, against an API started elsewhere on :5000
npm run stop:api                # stop an API left running from this checkout (it locks bin/ on Windows)
```

To run the pieces separately instead:

```sh
docker compose up -d                                      # PostgreSQL (postgres/postgres) and Redis (password "redis")
dotnet run --project src/backend/Source                   # the API
npm run dev -- --web-only                                 # the web app behind the dev proxy on :3000
```

Development and Testing apply migrations on startup. If you use your own PostgreSQL or Redis instead
of `docker compose`, update the connection strings in `src/backend/Source/appsettings.Development.json`
and `appsettings.Testing.json` (both git-ignored); to keep the containers with other passwords, set
`DEV_POSTGRES_PASSWORD` / `DEV_REDIS_PASSWORD` in the shell or the root `.env` to match.

### Seeded accounts

Every start creates, when absent:

- `admin`: the platform administrator. It manages tenants and editions, and belongs to no tenant.
- `tenantadmin`: the administrator of the Default tenant.

Their password was generated for this project. It is in the `Seed` section of
`src/backend/Source/appsettings.json` and the Development and Testing files. It is applied only when an
account is first created, so change it in the app after that.

## Tests

```sh
dotnet test src/backend/Tests          # needs PostgreSQL (not Redis); creates and deletes the `<database>_test` database
npm run gate                           # everything: build, backend tests, web lint/typecheck/tests, next build
npm run gate -- --fast                 # the same without the production web build
```

## Production (Docker)

`docker-compose.prod.yml` runs the API, the web app, PostgreSQL, Redis and Caddy (HTTPS), one origin
per host: the platform at `app.<DOMAIN>` (or `<PLATFORM_SUBDOMAIN>.<DOMAIN>`) and every tenant at
`<identifier>.<DOMAIN>`. Its values come
from the root `.env`, which was generated with random passwords, a JWT signing key and `DOMAIN=localhost`
(the platform at `https://app.localhost`). It is git-ignored; `.env.docker.example` documents every value.

Caddy obtains an HTTP-01 certificate per host — the platform's at startup, a tenant's on its first
visit, once the API confirms the host belongs to an existing tenant (`GET /api/tenants/tls-allowed`) —
so no DNS credentials are needed. Let's Encrypt issues at most 50 certificates per registered domain per
week; an installation expecting more new tenants than that should use a DNS-01 wildcard certificate
(see `docker/Caddyfile`).

```sh
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```

Before deploying for real:

- point two DNS A records at the server, the platform's host (`app.<DOMAIN>` by default) and `*.<DOMAIN>`;
- set `DOMAIN` to the domain the application lives under, `PLATFORM_SUBDOMAIN` to the platform's own
  subdomain (`app` unless it should live elsewhere, such as `demo`), `PUBLIC_URL` to
  `https://<PLATFORM_SUBDOMAIN>.<DOMAIN>` and
  `TENANT_URL_TEMPLATE` to `https://{tenant}.<DOMAIN>`;
- fill in the SMTP values;
- keep `.env` out of version control and back it up.

`SEED_ADMIN_PASSWORD` is the administrators' password in that stack. The API applies migrations on
startup there too, so add the initial migration first (see **Development**).

## Deploy to a VPS (Coolify)

`npm run deploy:vps` puts the application on a Linux VPS behind [Coolify](https://coolify.io), from
Windows, macOS or Linux. It needs Node and the OpenSSH client here (both ship with Windows 10+ and
macOS), and on the server only SSH access as root (or a sudo user) and two DNS A records pointing at
it: `app.<domain>` (the platform) and `*.<domain>` (every tenant). `--domain` is the domain the
application lives under, not the platform's host. The platform is served at `app.<domain>` unless
`--platform-subdomain` names another label: `--domain easyfornet.com --platform-subdomain demo` serves
it at `demo.easyfornet.com` (so that DNS record is `demo.<domain>`), and `demo` is then never a tenant's.

```sh
npm run deploy:vps -- --host 203.0.113.10 --domain example.com --email you@example.com
npm run deploy:vps -- --domain example.com              # later deploys: the other answers are remembered
EFN_DNS_TOKEN=<token> npm run deploy:vps -- --domain example.com --dns-provider cloudflare   # a *.example.com wildcard certificate
npm run deploy:vps -- --domain easyfornet.com --platform-subdomain demo   # the platform at demo.easyfornet.com
npm run deploy:vps -- --host 203.0.113.10 --check       # only report the server's prerequisites
npm run deploy:vps -- --help                            # every option (--user, --port, --identity, --platform-subdomain, --repo, --branch, --name, --dns-provider, --dns-token, --yes)
```

Over SSH it checks the server first (distribution, CPU, memory, disk, ports 80/443/8000, an existing
Docker) and stops if anything would fail; it also stops when `*.<domain>` has no DNS record (it resolves
a random label to tell). It then installs only what is missing: the tools it needs, a
swap file on a small server, firewall rules, Coolify itself and an API token. After that it creates
a Coolify project and application named after the domain, attaches the platform's host (`https://app.<domain>` by default) and
`https://*.<domain>`, fills in the environment variables, and deploys. Ubuntu, Debian, RHEL-family,
SUSE, Arch and Alpine servers are supported. Running it again redeploys and changes nothing else:
passwords and the JWT key are generated once, on the server, and are never overwritten.

Coolify's Traefik obtains the platform's certificate over HTTP-01, but cannot obtain one per tenant
host as it is first visited. `--dns-provider` (a [lego](https://go-acme.github.io/lego/dns/) provider
name: `cloudflare`, `digitalocean`, `duckdns`, `gandiv5`, `linode`, `vultr`, or any other with
the token given as `VARIABLE=token`) and an API token allowed to edit the domain's records let it hold
one `*.<domain>` wildcard certificate through DNS-01. Give the token as the `EFN_DNS_TOKEN` environment
variable: `--dns-token` works too, but stays in the shell's history and npm's debug log. The script never
prints, logs or saves it; on the server it lives in a root-only file Traefik reads. Without them only
HTTP-01 per-host certificates apply and tenant hosts are served with Traefik's default certificate —
pass the DNS flags, or deploy with `docker-compose.prod.yml`, whose Caddy issues them on demand.

Coolify builds from the Git repository with `docker-compose.coolify.yml`, so it deploys what is
**pushed** to the branch (the repository's base branch by default). A private repository gets a
read-only deploy key: the script adds it with `gh` when that is installed, or prints it for you to
add. Each domain is its own Coolify application, so several apps can share one server. The Coolify
dashboard is at `http://<server>:8000`; its login and the app's administrator password are saved on
the server in `/root/.efn-deploy/`.

## Spec-driven development

Save a spec under `specs/` and run `npm run loop`, or build one task with `npm run auto -- "<task>"`.
`specs/README.md` and `docs/AGENTIC_WORKFLOW.md` explain the loop; `agentic.config.json` configures it.
