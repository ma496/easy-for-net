# {{Name}}

An ASP.NET (FastEndpoints) API in `src/backend` and a Next.js web app in `src/frontend/web`, generated
with `dotnet efn`. `CLAUDE.md` describes the architecture; `.claude/skills` holds step-by-step guides.

## Prerequisites

- The .NET SDK pinned in `global.json`
- Node >= 24
- Docker (for PostgreSQL and Redis in development), or your own PostgreSQL on `localhost:5432` and
  Redis on `localhost:6379`

## Development

Once, from the root — `dotnet ef` needs the packages restored, and the API needs a migration to create
its database:

```sh
dotnet tool restore                                       # dotnet-ef
dotnet build {{Name}}.slnx
dotnet ef migrations add Initial --project src/backend/Source
```

Then, from the root, run the whole application:

```sh
npm run dev
```

It starts PostgreSQL and Redis with Docker (`docker-compose.yml`) when they are not already answering,
installs the web app's packages when they are missing, and runs the API (http://localhost:5000, Swagger
at `/swagger`) and the web app (http://localhost:3000) together in one terminal. **Ctrl+C stops both**; the
containers keep running until `docker compose down` (which keeps the data).

```sh
npm run dev -- --no-docker      # use the PostgreSQL and Redis you already run
npm run dev -- --api-only       # services + API, no web app
npm run dev -- --web-only       # the web app alone, against an API started elsewhere
npm run stop:api                # stop an API left running from this checkout (it locks bin/ on Windows)
```

To run the pieces separately instead:

```sh
docker compose up -d                                      # PostgreSQL (postgres/postgres) and Redis (password "redis")
dotnet run --project src/backend/Source                   # the API
cd src/frontend/web && npm install && npm run dev         # the web app
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
dotnet test src/backend/Tests          # needs PostgreSQL (not Redis); creates and deletes {{Name}}Test
npm run gate                           # everything: build, backend tests, web lint/typecheck/tests, next build
npm run gate -- --fast                 # the same without the production web build
```

## Production (Docker)

`docker-compose.prod.yml` runs the API, the web app, PostgreSQL, Redis and Caddy (HTTPS) from one
origin. Its values come from the root `.env`, which was generated with random passwords, a JWT signing
key and `DOMAIN=localhost`. It is git-ignored; `.env.docker.example` documents every value.

```sh
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```

Before deploying for real:

- set `DOMAIN` and `PUBLIC_URL` to the public host name;
- fill in the SMTP values;
- keep `.env` out of version control and back it up.

`SEED_ADMIN_PASSWORD` is the administrators' password in that stack. The API applies migrations on
startup there too, so add the initial migration first (see **Development**).

## Deploy to a VPS (Coolify)

`npm run deploy:vps` puts the application on a Linux VPS behind [Coolify](https://coolify.io), from
Windows, macOS or Linux. It needs Node and the OpenSSH client here (both ship with Windows 10+ and
macOS), and on the server only SSH access as root (or a sudo user) and the domain's DNS A record
pointing at it.

```sh
npm run deploy:vps -- --host 203.0.113.10 --domain app.example.com --email you@example.com
npm run deploy:vps -- --domain app.example.com          # later deploys: the other answers are remembered
npm run deploy:vps -- --host 203.0.113.10 --check       # only report the server's prerequisites
npm run deploy:vps -- --help                            # every option (--user, --port, --identity, --repo, --branch, --name, --yes)
```

Over SSH it checks the server first (distribution, CPU, memory, disk, ports 80/443/8000, an existing
Docker) and stops if anything would fail. It then installs only what is missing: the tools it needs, a
swap file on a small server, firewall rules, Coolify itself and an API token. After that it creates
a Coolify project and application named after the domain, attaches the domain (Coolify obtains the
HTTPS certificate), fills in the environment variables, and deploys. Ubuntu, Debian, RHEL-family,
SUSE, Arch and Alpine servers are supported. Running it again redeploys and changes nothing else:
passwords and the JWT key are generated once, on the server, and are never overwritten.

Coolify builds from the Git repository with `docker-compose.coolify.yml`, so it deploys what is
**pushed** to the branch (the repository's base branch by default). A private repository gets a
read-only deploy key: the script adds it with `gh` when that is installed, or prints it for you to
add. Each domain is its own Coolify application, so several apps can share one server. The Coolify
dashboard is at `http://<server>:8000`; its login and the app's administrator password are saved on
the server in `/root/.efn-deploy/`.

## Spec-driven development

Save a spec under `specs/` and run `npm run loop`, or build one task with `npm run auto -- "<task>"`.
`specs/README.md` and `docs/AGENTIC_WORKFLOW.md` explain the loop; `agentic.config.json` configures it.
