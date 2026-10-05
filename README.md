# FullStack Template

A full-stack template built with ASP.NET 10 and Next.js 16. It’s well-structured, includes essential features, and lets you build applications quickly. It also works seamlessly with AI, including built-in rules for generating various parts of the project.

## Prerequisites

- .NET 10 SDK
- Node.js 24 or later
- Git (the tool clones the template)
- Docker, for PostgreSQL and Redis in development — or your own PostgreSQL on `localhost:5432` and Redis on `localhost:6379`

## Installation

To install the EasyForNet tool globally using the .NET CLI, run the following command:

```sh
dotnet tool install --global EasyForNetTool
```

## Checking the Version

To check the version of the EasyForNet tool, run the following command:

```sh
dotnet efn -v
```

or

```sh
dotnet efn --version
```

## Create a New Project

To create a new project using the EasyForNet tool, run the following command:

```sh
dotnet efn cp -n {name} -o {path} -m {true|false}
```

- `-n {name}`: Specifies the name of the new project.
- `-o {path}`: (Optional) Specifies the output directory for the new project.
- `-m {true|false}`: (Optional) Enable multi-language support. Default is `false`. When `false`, only English language files are included. When `true`, all supported languages (English, Urdu, Chinese, Arabic, Hindi, Spanish, French, Russian) are included.

The new project is a git repository with one initial commit. The tool prints the seeded administrators'
password when it finishes (see **Seeded Accounts**).

## Run the Project

From the project's root directory (the one holding `{Name}.slnx`), prepare the backend once — `dotnet ef`
needs the packages restored, and the API needs a migration to create its database:

```sh
dotnet tool restore                                           # dotnet-ef
dotnet build {Name}.slnx
dotnet ef migrations add Initial --project src/backend/Source
```

Then run the whole application with one command:

```sh
npm run dev
```

It starts PostgreSQL and Redis with Docker (`docker-compose.yml`) when they are not already answering,
installs the web app's packages when they are missing, and runs the API and the web app together in one
terminal, each line prefixed by where it came from:

- API: [http://localhost:5000](http://localhost:5000), Swagger at [/swagger](http://localhost:5000/swagger/index.html)
- Web app: [http://localhost:3000](http://localhost:3000)

Press **Ctrl+C** to stop both. The containers keep running; `docker compose down` stops them (the data is kept).

```sh
npm run dev -- --no-docker      # use the PostgreSQL and Redis you already run
npm run dev -- --api-only       # services + API, no web app
npm run dev -- --web-only       # the web app alone, against an API started elsewhere
npm run stop:api                # stop an API left running from this project (it locks bin/ on Windows)
```

To run the pieces separately instead:

```sh
docker compose up -d                          # PostgreSQL (postgres/postgres) and Redis (password "redis"), on the ports DEV_POSTGRES_PORT / DEV_REDIS_PORT name (default 5432 / 6379)
dotnet run --project src/backend/Source       # the API
cd src/frontend/web && npm install && npm run dev
```

Development and Testing apply migrations on startup. Any other environment applies them only when
`Database:ApplyMigrationsOnStartup` is set (the Docker stacks below set it), or explicitly:

```sh
dotnet ef database update --project src/backend/Source
```

## Seeded Accounts

Every start creates, when absent:

- `admin`: the platform administrator. It manages tenants and editions, and belongs to no tenant.
- `tenantadmin`: the administrator of the Default tenant.

Their password is generated for each project and printed when it is created. It is in the `Seed`
section of `src/backend/Source/appsettings.json` (and of the Development and Testing files) and
applied only when an account is first created, so change it in the app after that.

## Change Connection Strings

Go to `{name}/src/backend/Source` directory. By default, the EasyForNet sets up connection strings for PostgreSQL in the `appsettings.json`, `appsettings.Development.json` and `appsettings.Testing.json` files (the last two are git-ignored). Development and Testing connect as `postgres` / `postgres` and to Redis with the password `redis`, the `docker compose` defaults; `appsettings.json` keeps a `{password}` placeholder. To use another PostgreSQL or Redis, follow these steps:

1. Open the `appsettings.Development.json` file. Update the `DefaultConnection`, `Redis` and `Hangfire` connection strings with your connection details:

    ```json
    {
      "ConnectionStrings": {
        "DefaultConnection": "Host=your_host;Database=your_db;Username=your_user;Password=your_password",
        "Redis": "your_host:6379,password=your_redis_password"
      },
      "Hangfire": {
        "Storage": {
          "ConnectionString": "Host=your_host;Database=your_db;Username=your_user;Password=your_password"
        }
      }
    }
    ```

2. Open the `appsettings.Testing.json` file. Update the PostgreSQL connection strings the same way (the test run creates and deletes its own database, and needs no Redis):

    ```json
    {
      "ConnectionStrings": {
        "DefaultConnection": "Host=your_host;Database=your_test_db;Username=your_user;Password=your_password"
      },
      "Hangfire": {
        "Storage": {
          "ConnectionString": "Host=your_host;Database=your_test_db;Username=your_user;Password=your_password"
        }
      }
    }
    ```

To keep using the Docker containers with other passwords, set `DEV_POSTGRES_PASSWORD` / `DEV_REDIS_PASSWORD`
(in the shell or the root `.env`) to the same values. A PostgreSQL password takes effect only when its
volume is first created. Likewise `DEV_POSTGRES_PORT` / `DEV_REDIS_PORT` move the containers off a
PostgreSQL or Redis already installed on the machine; the connection strings must name the same ports.
`dotnet efn cp` does this itself when it finds 5432 or 6379 taken.

## Run the Tests

From the project root (the backend tests need PostgreSQL, not Redis):

```sh
dotnet test src/backend/Tests       # backend tests
npm run gate                        # everything: build, backend tests, web lint/typecheck/tests, next build
npm run gate -- --fast              # the same without the production web build
```

## Docker (Production)

`docker-compose.prod.yml` builds and runs the whole application — API, web app, PostgreSQL, Redis, and
Caddy serving both apps from one HTTPS origin (certificates are obtained automatically for `DOMAIN`):

```sh
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```

A generated project already has the root `.env` (git-ignored): random database, Redis and administrator
(`SEED_ADMIN_PASSWORD`) passwords, a JWT key, and `DOMAIN=localhost`, so the stack starts as generated.
Before deploying for real, set `DOMAIN`, `PUBLIC_URL` and the SMTP values. `.env.docker.example`
documents every value; copy it to `.env` and fill it in if the file is missing.

The API applies migrations on startup, so add the initial migration first (see **Run the Project**).
`NEXT_PUBLIC_API_URL` is built into the web image from `PUBLIC_URL`, so changing the domain means
rebuilding. Uploaded files and the Data Protection keys (which encrypt secret settings) live in named
volumes; back up those and the database volume.

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

## Features

- **Authentication & Sessions** – JWT bearer or cookie sign-in with refresh tokens; every session lives in Redis and is revoked at once when the user's access changes.
- **Permissions-Based Authorization** – Fine-grained, hierarchical permissions enforced on every endpoint and mirrored in the web app.
- **Role & Permission Management** – Define roles and assign permissions dynamically through the frontend.
- **User Management** – Pages and endpoints for user CRUD, changing passwords, and forgotten/reset passwords.
- **Multi-Tenancy** – Tenants with isolated data, memberships, tenant switching, and a platform administrator above them.
- **Editions & Feature Management** – Plans that switch features on or off and set limits (users per tenant, upload size) per tenant.
- **Settings** – Typed settings the platform and each tenant can override at run time, with encrypted secrets.
- **Localization** – Translations served by the API, with per-tenant and platform overrides and enabled languages edited in the app; right-to-left support.
- **File Management** – Uploads with plan-based size limits.
- **Real-Time Notifications** – In-app notifications pushed over SignalR.
- **Self-Hosted Background Email Service** – Send emails directly from your own server without relying on third-party services.
- **Automatic Cleanup Jobs** (Hangfire)
  - `delete-expired-auth-tokens` – A recurring job that runs once per day to remove expired authentication tokens. The schedule can be customized.
  - `delete-expired-tokens` – A recurring job that runs once per day to remove expired tokens used for the "Forgot Password" functionality. The schedule can be customized.
  - `delete-expired-notifications` – A recurring job that runs once per day to remove notifications older than `Notifications:RetentionDays`.
- **AI-Ready** – A `CLAUDE.md`, step-by-step skills, specialised agents, guard hooks, and a spec-driven task loop (`npm run loop`) for building features with Claude Code.

## Custom Development

For custom development, you can email me at `easyfornet@outlook.com`

## Star the Project

If you find this project helpful and appreciate the effort put into creating a robust backend template and tool for creating fast endpoints, please consider giving it a star on GitHub. Your support helps make the project more visible to others who might benefit from it.

⭐ Star this [repository](https://github.com/ma496/EasyForNet) to show your support! ⭐

Your stars motivate me to:

- Add more features and improvements
- Maintain documentation
- Provide better support

Thank you for your support! 👍

## Documentation

Check out the [Documentation](https://github.com/ma496/EasyForNet/wiki) for additional information.
