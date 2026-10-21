Serve each tenant on its own subdomain and the platform on app.<domain>, with one session per host

Today the URL never names a tenant. The tenant is chosen at sign-in, from the optional
`TenantIdentifier` field or the account's single membership, and is stored in the session
(`tenant_id`). It changes through `POST /tenants/switch` / `exit`, and the header shows a tenant
switcher.

After this change:
- `acme.web.com` is tenant `acme`.
- `app.web.com` is the platform host: platform administration and sign-up of new tenants.
- The sign-in page has no tenant field, and the header shows no tenant.

Several tenants can be used side by side in one browser. This works because the browser reaches the
API **same-origin**, as `<host>/api` and `<host>/hubs`. The API's cookies (`.AspNetCore.Cookies`,
`refreshToken`) carry no `Domain` attribute, so each host keeps its own cookie pair and therefore
its own session. Changing tenant means going to that tenant's address.

**The host chooses, it never grants.** The session stays the only authority, and membership is
still checked at sign-in. The host decides only:
- which tenant a sign-in targets,
- which tenant's language/text overrides an anonymous visitor sees,
- which session a request is allowed to act with.

**No database query is added to ordinary requests.** Only the few anonymous endpoints listed below
look a tenant up by identifier.

## Scope

### Configuration

- **`Web:DefaultDomain`** is the platform origin: `https://app.web.com`, or `http://app.localhost:3000`
  in development.
- **New `Web:TenantOriginTemplate`** is the shape of a tenant's origin: `https://{tenant}.web.com`, or
  `http://{tenant}.localhost:3000` in development. It is validated on start:
  - it contains exactly one `{tenant}`;
  - that `{tenant}` is the first host label;
  - it parses as an absolute URI once substituted.
- An **empty template turns host resolution off**: every host resolves to `None` and the API behaves
  exactly as today.
  - The Testing host leaves it empty, so the existing suite and bearer clients on unknown hosts are
    unaffected.
  - The defaults live in `Program.cs` / `appsettings.json`, because `appsettings.Testing.json` is not in
    source control.
  - `appsettings.json` and Development use the `localhost` values above.

### API: host resolution (Tenancy slice, `Features/Tenancy/Core/Hosting/`)

- **`ITenantHostResolver`** (`[AllowOutside]`) reads `HttpContext.Request.Host` against the two
  settings. It is pure parsing with no database access, and returns one of:
  - `None` — resolution is off, or the host is unknown;
  - `Platform`;
  - `Tenant(identifier)` — the label only.
- It also provides `TenantUrl(identifier)` and `PlatformUrl()`. Every place that needs a link to the web
  app uses these.
- **A lookup by identifier happens only where an anonymous endpoint needs one:**
  - sign-in;
  - sign-up;
  - `GET /tenants/host`;
  - `GET /tenants/tls-allowed`;
  - anonymous `localization/resources`.
- These share one query: the `IdentifierNormalized` query now inline in
  `TokenEndpoint.ResolveNamedTenantAsync`, moved to `ITenantService.FindByIdentifierAsync` with
  `AcrossAllTenants`. There is no cache.
- `Program.cs` adds `ForwardedHeaders.XForwardedHost` to the forwarded headers. It is still trusted only
  from known proxies, or from any proxy under `ForwardedHeaders:TrustAllProxies`. This lets the Next
  server's own fetches name the visitor's host.

### API: the session carries the tenant identifier

- `SessionRecord` gains `TenantIdentifier`.
  - `ISessionIssuer` fills it in when it creates a session, at sign-in, refresh and switch/exit, all of
    which already query the database.
  - `SessionClaims.Project` exposes it as a `tenant_identifier` claim.
- **New `TenantHostSessionMiddleware`**, straight after `SessionStoreUnavailableMiddleware`, so it
  covers both endpoints and the notification hub. For an authenticated request:
  - on a `Tenant(identifier)` host, the session's `tenant_identifier` must equal the host's label;
  - on the `Platform` host, the session must have no tenant;
  - otherwise it answers **403 `tenantHostMismatch`**;
  - `None` skips the check.
  - It is a string comparison over the already-projected claims, with no further reads.
- **Changing a tenant's `Identifier` ends every session in that tenant.** `TenantUpdateEndpoint` calls
  `ISessionRevocationService` the same way suspension does. Add this row to the revocation table in
  `CLAUDE.md`.

### API: sign-in, sign-up, switching and emails

- **Sign-in (`TokenEndpoint`)**:
  - **Tenant host:** the tenant is the host's tenant, through the existing named-tenant path with its
    `tenantNotFound` / `notTenantMember` / `tenantSuspended` checks. This applies to platform accounts
    too. A request `TenantIdentifier` that names a different tenant is refused with
    `tenantHostMismatch`.
  - **Platform host:** platform accounts sign in with no tenant. Ordinary accounts are refused with the
    new `tenantHostRequired`.
  - **`None`:** unchanged. Bearer clients and tests keep the `TenantIdentifier` request field.
- **Sign-up:** allowed on the platform host, or under `None`. On a tenant host it answers
  `tenantHostMismatch`. The verification email links to the **new tenant's** origin.
- **`ResendVerifyEmailEndpoint` and `ForgetPasswordEndpoint`:** build their links from the request's
  own resolved host, falling back to `Web:DefaultDomain` under `None`.
- **`POST /tenants/switch` and `/tenants/exit`:** kept for bearer clients. On a resolved host they answer
  `tenantHostMismatch`.
- **`GetInfoEndpoint`:** adds `url` to every tenant it lists and returns `platformUrl`, so the web app
  never has a domain baked in.
- **New anonymous `GET /tenants/host`:** returns
  - `{ kind: "platform", platformUrl }`, or
  - `{ kind: "tenant", name, identifier, status, platformUrl }`, or
  - 404 `tenantNotFound`.
- **New anonymous `GET /tenants/tls-allowed?domain=`:** answers 200 only for the platform host or an
  existing, non-deleted tenant's host, and 404 otherwise. It is Caddy's on-demand TLS `ask` endpoint.
- **Anonymous `GET /localization/resources/{culture}` on a tenant host:** serves that tenant's overrides,
  through an explicit tenant argument to `ILocalizationService`. No general tenant scope is opened for
  anonymous requests. Authenticated callers are unchanged.

### API: reserved identifiers and the hub

- `TenantValidationRules` refuses these identifiers with a new `ErrorCodes.TenantIdentifierReserved`
  (`error.server.tenantIdentifierReserved` in every resource file):
  - `app`, `www`, `api`, `admin`, `mail`, `smtp`, `static`, `cdn`, `status`, `help`, `docs`, `hangfire`;
  - the platform host's own label.
  - The rule sits in the existing `TenantIdentifier()` rule, so it applies to sign-up, tenant create and
    tenant update.
- The hub's Origin check no longer uses the fixed `WebSocketOptions.AllowedOrigins` list:
  - an upgrade's `Origin` must equal the request's own scheme and host, or be one of `AllowedDomains()`;
  - an upgrade with no `Origin` still passes.
- The CORS policy stays as it is.
- New error codes get English and translated messages in every resource file.

### Web (`src/frontend/web`)

- **`config/environment.ts`:**
  - `apiUrl` defaults to `/api`;
  - `serverApiUrl` is `API_INTERNAL_URL`, now required;
  - update `.env.example` and the `Dockerfile` build argument to match.
- **`getDictionary` (`i18n/server.ts`)**, and any other server-side API fetch, forwards
  `X-Forwarded-Host` with the incoming `host` header, through one shared helper.
- **Sign-in form:**
  - Remove the tenant field, its validation and its value.
  - The heading names the workspace from `GET /tenants/host` ("Sign in to Acme").
  - An unknown host shows "workspace not found" with a link to the platform.
  - `tenantHostRequired` is explained the same way.
  - Remove translation keys that nothing uses any more.
- **Header:** remove `TenantSwitcher`. Nothing in the header names the current tenant.
- **Other workspaces:**
  - The user menu lists the account's other tenants as plain links to each `tenant.url`, opening in a
    new tab. Platform accounts also get a link to `platformUrl`.
  - `select-tenant-view.tsx` becomes the same list of links.
  - The tenants table's "enter" action becomes a link.
- **Delete the in-place switch path:**
  - `hooks/use-tenant-switch.ts`;
  - `components/custom/tenant-switcher.tsx`;
  - the `tenantSwitch` / `tenantExit` mutations;
  - `leaveForTenantChange` / `takeTenantChangeNotice` in `store/tenant-cache.ts`;
  - the post-sign-in chooser landing in `lib/utils/tenant-routing.ts`.
  - Rewrite the source-chain tests (`store/tenant-cache.test.ts`, `select-tenant-view.test.ts`,
    `i18n/tenant-screens.test.ts`, `tenant-routing.test.ts`) so they pin the new rule: nothing in the
    web app calls `/tenants/switch` or `/tenants/exit`, and tenant links come from `tenant.url`.
- **Sign-up form:**
  - Shown on the platform host; on a tenant host it links to the platform's sign-up.
  - The identifier field previews the resulting address and shows the reserved-name error.
  - The success view links to the new workspace's sign-in page.
- **Unchanged:** `lib/notifications/hub-url.ts` and `proxy.ts`. The hub URL already follows the page's
  origin, and locale and auth routing do not depend on the host.

### Development

- **New `scripts/dev-proxy.mjs`**, a dependency-free Node `http` proxy that mirrors `docker/Caddyfile`:
  - `/api/*`, `/hubs/*`, `/hangfire*` and `/health` go to the API on `:5000`;
  - everything else goes to `next dev` on `:3001`;
  - WebSocket upgrades are piped for both the hub and Next HMR;
  - the original `Host` header is kept.
- **`scripts/dev.mjs`:**
  - starts the proxy on `:3000` and the web app on `:3001`;
  - keeps `--api-only` / `--web-only` working;
  - prints `http://app.localhost:3000` and `http://default.localhost:3000`.

### Deployment

- **`.env.docker.example`:** `DOMAIN=example.com`, `PUBLIC_URL=https://app.example.com`,
  `TENANT_URL_TEMPLATE=https://{tenant}.example.com`.
- **`docker-compose.prod.yml` and `docker-compose.coolify.yml`:**
  - pass `Web__TenantOriginTemplate`;
  - build the web app with `NEXT_PUBLIC_API_URL=/api`;
  - stay in step, as `scripts/tests/deploy-vps.test.mjs` requires.
- **`docker/Caddyfile`:**
  - serves `app.{$DOMAIN}` and `*.{$DOMAIN}`;
  - uses on-demand TLS with `ask http://api:8080/api/tenants/tls-allowed`, so HTTP-01 certificates are
    issued per host and no DNS-provider credentials are needed;
  - a comment documents Let's Encrypt's 50 certificates per registered domain per week, and DNS-01
    wildcards for larger installations.
- **Coolify:**
  - `Caddyfile.coolify` already accepts any host;
  - `scripts/deploy/vps/remote.sh` attaches `https://*.<domain>` beside `https://app.<domain>`;
  - `scripts/deploy-vps.mjs` takes `--dns-provider` / `--dns-token` for Traefik's DNS-01 wildcard
    certificate;
  - its DNS check also resolves a random label to confirm the wildcard record.
- **Documentation:** both paths need `A app.<domain>` and `A *.<domain>` records.

### Documentation

- **`CLAUDE.md`:**
  - the Auth section: how sign-in picks the tenant, the host rule, the new revocation row, and the new
    error codes;
  - the frontend section: same-origin `/api`, and switching by navigation;
  - the `npm run dev` description.
- **Skills:** `multi-tenancy`, `frontend-page`, `new-project` (the development URLs). Keep them generic,
  because they ship to generated projects.

## Out of scope — do not touch

- **Custom domains per tenant** (`app.acme.com`).
- **Sharing a sign-in across hosts** (a `Domain=` cookie, or a hand-off token from the platform host).
  Each host signs in on its own, on purpose.
- **Permissions, `SessionGrants` and plan gating.** The host never grants anything.
- **The `CreateProjectGenerator` locale and examples logic.** Only touch the generator if a new root
  file must ship.

## Done when

- `npm run verify -- --autostart` passes, and the existing backend suite is green with the template
  empty.
- Backend integration tests, in their own `[Collection("TenantHosting")]`, enable the template through
  the fixture's configuration and send `Host` headers. They cover:
  - sign-in on a tenant host by a member and a non-member;
  - a suspended tenant;
  - an unknown host;
  - the platform host with a platform account and with an ordinary account;
  - a `TenantIdentifier` that disagrees with the host;
  - a session used on another tenant's host answering 403 `tenantHostMismatch`, on an endpoint and on
    the hub upgrade;
  - renaming an identifier ending the tenant's sessions;
  - reserved identifiers refused at sign-up, tenant create and tenant update;
  - anonymous `resources/{culture}` returning the host tenant's overrides;
  - email links pointing at the right origin (`RecordingEmailTransport`);
  - `tls-allowed`;
  - `GET /tenants/host` for each kind.
- Web: `npm run lint`, `npx tsc --noEmit` and `npm run test` pass, with the rewritten source-chain tests.
- By hand with `npm run dev`:
  - `http://default.localhost:3000/signin` has no tenant field and signs `tenantadmin` in to the default
    tenant.
  - A second tenant created from `http://app.localhost:3000` can be signed in to in another tab, and
    both tabs stay signed in to their own tenant. Signing out of one leaves the other signed in, and
    each receives its own notifications.
  - The header shows no tenant, and the user menu's workspace links open the other host.
  - Signing up with the identifier `app` is refused.
