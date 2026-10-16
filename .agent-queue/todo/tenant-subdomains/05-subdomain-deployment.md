Serve the platform and every tenant subdomain in production with on-demand TLS
Depends-on: 01-same-origin-api-and-dev-proxy, 03-tenant-host-endpoints-and-links

The API resolves `app.<domain>` and `<tenant>.<domain>` once `Web:TenantOriginTemplate` is set, and answers Caddy's on-demand TLS question at `GET /api/tenants/tls-allowed`. Both deployment paths still serve a single host. This task makes them serve the platform host and every tenant subdomain: the environment names the domain and the tenant template, the API receives the template, Caddy issues a certificate per host on demand (HTTP-01, no DNS credentials), and the Coolify path attaches the wildcard and can obtain a DNS-01 wildcard certificate through Traefik.

## Scope
- **`.env.docker.example`:** `DOMAIN=example.com`, `PUBLIC_URL=https://app.example.com`, `TENANT_URL_TEMPLATE=https://{tenant}.example.com`, with comments. If `CreateProjectGenerator` writes the generated project's `.env` from this file by rewriting named keys, keep that working and its tool tests green.
- **`docker-compose.prod.yml` and `docker-compose.coolify.yml`:** pass `Web__TenantOriginTemplate` (from `TENANT_URL_TEMPLATE`) to the API beside `Web__DefaultDomain`, and `DOMAIN` to Caddy where it needs it; stay in step as `scripts/tests/deploy-vps.test.mjs` requires (extend that test for the new variables).
- **`docker/Caddyfile`:** serves `app.{$DOMAIN}` and `*.{$DOMAIN}` with the existing routes (`/api/*`, `/hubs/*`, `/hangfire*`, `/health` → API, the rest → web), the original `Host` kept; global `on_demand_tls { ask http://api:8080/api/tenants/tls-allowed }` with `tls { on_demand }` on the site, so HTTP-01 certificates are issued per host. A comment documents Let's Encrypt's 50 certificates per registered domain per week and DNS-01 wildcards for larger installations. `X-Forwarded-Host` reaches the API (the API trusts it under `ForwardedHeaders:TrustAllProxies`, which prod compose already sets).
- **Coolify:** `docker/Caddyfile.coolify` already accepts any host — confirm, change nothing if so. `scripts/deploy/vps/remote.sh` attaches `https://*.<domain>` beside `https://app.<domain>`. `scripts/deploy-vps.mjs` takes `--dns-provider` / `--dns-token` for Traefik's DNS-01 wildcard certificate (optional; without them it explains that only HTTP-01 per-host certificates apply, and the token is never printed or logged); its DNS check also resolves a random label to confirm the `*.<domain>` record, and refuses to continue with a clear message when it does not resolve. Tests in `scripts/tests` for the new flags and the random-label check.
- **Docs:** both deployment paths need `A app.<domain>` and `A *.<domain>` records — in `README.md`, `tool/EasyForNetTool/README.md`, `tool/EasyForNetTool/new-project-readme.md`, and the deploy section of `CLAUDE.md` / `tool/EasyForNetTool/new-project-claude.md` where they describe `npm run deploy:vps` or the domain.

## Out of scope — do not touch
- `NEXT_PUBLIC_API_URL=/api` in the compose files, the web `Dockerfile` and `.env.example` — done by `01-same-origin-api-and-dev-proxy`; leave as they are.
- `scripts/dev.mjs`, `scripts/dev-proxy.mjs` — `01-same-origin-api-and-dev-proxy`.
- Backend code and `appsettings*.json` — `02-tenant-host-resolution-and-session-binding`, `03-tenant-host-endpoints-and-links`.
- The web app — `04-web-workspaces-by-host`.
- Custom domains per tenant; a cross-host sign-in.
- `CreateProjectGenerator`'s file list, unless a new root file must ship.

## Done when
- `npm run verify` passes, including `scripts/tests/deploy-vps.test.mjs`, the new deploy-script tests and the tool tests.
- `docker compose -f docker-compose.prod.yml config` and the Coolify file render with the new variables and no unset-variable warnings when `.env` is filled from `.env.docker.example`.
- The Caddyfile validates (`caddy validate` in the Caddy image, if Docker is available) and routes `/api`, `/hubs`, `/hangfire*` and `/health` to the API for both `app.{$DOMAIN}` and a tenant subdomain.
- `npm run deploy:vps -- --help` (or its usage text) documents `--dns-provider` / `--dns-token`, and the DNS check fails clearly when `*.<domain>` has no record.
- Nothing a deployment currently relies on regresses: the single-host health check, the API's `/health`, and the deploy-vps flow without the new flags.
