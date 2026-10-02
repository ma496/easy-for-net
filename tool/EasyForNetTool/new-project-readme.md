# {{Name}}

An ASP.NET (FastEndpoints) API in `src/backend` and a Next.js web app in `src/frontend/web`, generated
with `dotnet efn`. `CLAUDE.md` describes the architecture; `.claude/skills` holds step-by-step guides.

## Prerequisites

- The .NET SDK pinned in `global.json`
- Node >= 24
- Docker (for PostgreSQL and Redis in development), or your own PostgreSQL on `localhost:5432` and
  Redis on `localhost:6379`

## Development

```sh
docker compose up -d                                      # PostgreSQL (postgres/postgres) and Redis (password "redis")
dotnet tool restore                                       # dotnet-ef
dotnet build {{Name}}.slnx                                # restores packages, which dotnet ef needs first
dotnet ef migrations add Initial --project src/backend/Source
dotnet run --project src/backend/Source                   # http://localhost:5000, Swagger at /swagger
```

```sh
cd src/frontend/web
npm install
npm run dev                                               # http://localhost:3000
```

Development and Testing apply migrations on startup. If you use your own PostgreSQL or Redis instead
of `docker compose`, update the connection strings in `src/backend/Source/appsettings.Development.json`
and `appsettings.Testing.json` (both git-ignored).

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

`SEED_ADMIN_PASSWORD` is the administrators' password in that stack.

## Spec-driven development

Save a spec under `specs/` and run `npm run loop`, or build one task with `npm run auto -- "<task>"`.
`specs/README.md` and `docs/AGENTIC_WORKFLOW.md` explain the loop; `agentic.config.json` configures it.
