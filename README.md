# FullStack Template

A full-stack template built with ASP.NET 10 and Next.js 16. It’s well-structured, includes essential features, and lets you build applications quickly. It also works seamlessly with AI, including built-in rules for generating various parts of the project.

## Prerequisites

- .NET 10.0
- PostgreSQL
- Node.js

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

## Build Backend

To build the backend project, navigate to the `{name}/src/backend` directory and run the following command:

```sh
dotnet tool restore
dotnet build
```

Production deployments should apply migrations explicitly before starting the API:

```bash
dotnet ef database update --project src/backend/Source/Backend.csproj
```

Automatic migrations remain enabled for Development and Testing. Set
`Database:ApplyMigrationsOnStartup` explicitly if a different environment needs that behavior.

## Docker

**Development** runs only PostgreSQL and Redis in containers; the API and the web app run on the host
(`dotnet run`, `npm run dev`). From the project root:

```sh
docker compose up -d        # PostgreSQL on localhost:5432 (user/password postgres), Redis on localhost:6379 (password redis)
docker compose down         # stop, keeping the data
```

`npm run dev` from the root starts these containers when PostgreSQL or Redis is not already answering, then
runs the API and the web app together in one terminal (Ctrl+C stops both, the containers keep running).

To use other passwords, set `DEV_POSTGRES_PASSWORD` / `DEV_REDIS_PASSWORD` (in the shell or the root
`.env`) and put the same values in `appsettings.Development.json`: the PostgreSQL connection strings
below, and `ConnectionStrings:Redis` (`localhost:6379,password=<password>`). A PostgreSQL password
takes effect only when its volume is first created.

**Production** builds and runs the whole application — API, web app, PostgreSQL, Redis, and Caddy
serving both apps from one HTTPS origin (certificates are obtained automatically for `DOMAIN`):

```sh
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```

A generated project already has the root `.env` (git-ignored): random database, Redis and administrator
(`SEED_ADMIN_PASSWORD`) passwords, a JWT key, and `DOMAIN=localhost`, so the stack starts as generated.
Before deploying for real, set `DOMAIN`, `PUBLIC_URL` and the SMTP values. `.env.docker.example`
documents every value; copy it to `.env` and fill it in if the file is missing.

The API applies migrations on startup, so add the initial migration first (see **Add Migration**).
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
npm run deploy:vps -- --help                            # every option (--user, --port, --identity, --repo, --branch, --name)
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

## Change Connection Strings

Go to `{name}/src/backend/Source` directory. By default, the EasyForNet sets up connection strings for PostgreSQL in the `appsettings.json`, `appsettings.Development.json` and `appsettings.Testing.json` files. Development and Testing connect as `postgres` / `postgres`, the `docker compose` default; `appsettings.json` keeps a `{password}` placeholder. To use another PostgreSQL, follow these steps:

1. Open the `appsettings.Development.json` file. Update the `DefaultConnection` and `Hangfire` connection strings with your PostgreSQL connection details:

    ```json
    {
      "ConnectionStrings": {
        "DefaultConnection": "Host=your_host;Database=your_db;Username=your_user;Password=your_password"
      },
      "Hangfire": {
        "Storage": {
          "ConnectionString": "Host=your_host;Database=your_db;Username=your_user;Password=your_password"    
        } 
      }
    }
    ```

2. Open the `appsettings.Testing.json` file. Update the `DefaultConnection` string with your PostgreSQL connection details:

    ```json
    {
      "ConnectionStrings": {
        "DefaultConnection": "Host=your_host;Database=your_db;Username=your_user;Password=your_password"
      }
    }
    ```

## Add Migration

Go to `{name}/src/backend/Source` directory and run the following commands. Build first: `dotnet ef` fails on a project whose packages were never restored.

```sh
dotnet build
dotnet ef migrations add Initial
```

## Run the Backend Project

To run the project, navigate to the `{name}/src/backend/Source` directory and execute the following command:

```sh
dotnet run
```

Once the project is running, open your browser and go to [http://localhost:5000/swagger/index.html](http://localhost:5000/swagger/index.html) to view the Swagger documentation for the endpoints.

## Install the Frontend Dependencies

To install the frontend dependencies, navigate to the `{name}/src/frontend/web` directory and execute the following command:

```sh
npm install
```

## Run the Frontend Project

To run the project, navigate to the `{name}/src/frontend/web` directory and execute the following command:

```sh
npm run dev
```

Seeded accounts:

- Platform administrator (manages tenants, belongs to no tenant): `admin`
- Default tenant administrator: `tenantadmin`

Their password is generated for each project and printed when it is created. It is in the `Seed`
section of `src/backend/Source/appsettings.json` and applied only when an account is first created.

## Run the Tests

To run the tests, navigate to the `{name}/src/backend/Tests` directory and execute the following command:

```sh
dotnet test
```

## Features  

- **JWT Authentication & Refresh Tokens** – Secure authentication with built-in refresh token handling.  
- **Permissions-Based Authorization** – Fine-grained access control using flexible permissions, ensuring users can only perform authorized actions.  
- **Role & Permission Management** – Define roles and assign permissions dynamically through frontend.  
- **User Management** – Includes endpoints and pages for user CRUD operations, changing passwords, and handling forgotten/reset passwords.   
- **Localization** – Support multiple languages.  
- **Self-Hosted Background Email Service** – Send emails directly from your own server without relying on third-party services.  
- **Automatic Token Cleanup Jobs**  
  - `delete-expired-auth-tokens` – A recurring job that runs once per day to remove expired authentication tokens. The schedule can be customized.  
  - `delete-expired-tokens` – A recurring job that runs once per day to remove expired tokens used for the "Forgot Password" functionality. The schedule can be customized.  

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
