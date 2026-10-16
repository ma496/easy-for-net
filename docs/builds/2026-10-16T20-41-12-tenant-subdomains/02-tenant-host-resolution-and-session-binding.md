# Resolve the tenant from the request host and bind each session to its host

| | |
|---|---|
| **Task** | `tenant-subdomains/02-tenant-host-resolution-and-session-binding` |
| **Landed** | 2026-10-16 |
| **Commit** | the one whose message carries `Task: tenant-subdomains/02-tenant-host-resolution-and-session-binding` |
| **Verification** | `npm run verify` — the static gate, plus whichever live checks the diff demanded |

## What changed

| File | + | − |
|------|---|---|
| `.claude/skills/multi-tenancy/SKILL.md` | 18 | 3 |
| `CLAUDE.md` | 7 | 4 |
| `src/backend/Source/ErrorHandling/ErrorCodes.cs` | 2 | 0 |
| `src/backend/Source/Features/Identity/Core/AuthTokenService.cs` | 1 | 0 |
| `src/backend/Source/Features/Identity/Core/ClaimConstants.cs` | 7 | 0 |
| `src/backend/Source/Features/Identity/Core/Sessions/SessionClaims.cs` | 6 | 0 |
| `src/backend/Source/Features/Identity/Core/Sessions/SessionIssuer.cs` | 7 | 2 |
| `src/backend/Source/Features/Identity/Core/Sessions/SessionRecord.cs` | 8 | 0 |
| `src/backend/Source/Features/Identity/Core/TenantAuthorizationService.cs` | 7 | 3 |
| `src/backend/Source/Features/Identity/Endpoints/Account/TokenEndpoint.cs` | 81 | 28 |
| `src/backend/Source/Features/Identity/Endpoints/Account/TokenService.cs` | 15 | 11 |
| `src/backend/Source/Features/Localization/Core/Resources/ar.json` | 2 | 0 |
| `src/backend/Source/Features/Localization/Core/Resources/en.json` | 2 | 0 |
| `src/backend/Source/Features/Localization/Core/Resources/es.json` | 2 | 0 |
| `src/backend/Source/Features/Localization/Core/Resources/fr.json` | 2 | 0 |
| `src/backend/Source/Features/Localization/Core/Resources/hi.json` | 2 | 0 |
| `src/backend/Source/Features/Localization/Core/Resources/ru.json` | 2 | 0 |
| `src/backend/Source/Features/Localization/Core/Resources/ur.json` | 2 | 0 |
| `src/backend/Source/Features/Localization/Core/Resources/zh.json` | 2 | 0 |
| `src/backend/Source/Features/Notifications/Core/Push/NotificationHubRegistration.cs` | 13 | 16 |
| `src/backend/Source/Features/Tenancy/Core/TenancyDtos.cs` | 13 | 1 |
| `src/backend/Source/Features/Tenancy/Core/TenantService.cs` | 34 | 0 |
| `src/backend/Source/Features/Tenancy/Endpoints/Tenants/TenantExitEndpoint.cs` | 11 | 2 |
| `src/backend/Source/Features/Tenancy/Endpoints/Tenants/TenantSwitchEndpoint.cs` | 13 | 3 |
| `src/backend/Source/Features/Tenancy/Endpoints/Tenants/TenantUpdateEndpoint.cs` | 11 | 4 |
| `src/backend/Source/Features/Tenancy/TenancyFeature.cs` | 5 | 0 |
| `src/backend/Source/Middleware/SessionStoreUnavailableMiddleware.cs` | 2 | 22 |
| `src/backend/Source/Program.cs` | 18 | 2 |
| `src/backend/Source/Settings/WebSetting.cs` | 14 | 2 |
| `src/backend/Source/appsettings.json` | 3 | 2 |
| `src/backend/Tests/Fakes/TestDoubles.cs` | 17 | 0 |
| `src/backend/Tests/FeatureManagement/PlanChangeSessionRevocationTests.cs` | 2 | 2 |
| `src/backend/Tests/Features/Identity/Sessions/SessionClaimsTests.cs` | 26 | 0 |
| `src/backend/Tests/Features/Notifications/Core/HubProbe.cs` | 16 | 6 |
| `src/backend/Tests/Features/Notifications/Core/NotificationHubTests.cs` | 32 | 61 |
| `tool/EasyForNetTool/new-project-claude.md` | 7 | 4 |
| `.claude/memory/lessons/turn-host-resolution-on-in-a-test-with-the-x-test-tenant-hos.md` | 9 | 0 |
| `src/backend/Source/Features/Notifications/Core/Push/NotificationHubOriginMiddleware.cs` | 73 | 0 |
| `src/backend/Source/Features/Tenancy/Core/Hosting/TenantHost.cs` | 47 | 0 |
| `src/backend/Source/Features/Tenancy/Core/Hosting/TenantHostMap.cs` | 183 | 0 |
| `src/backend/Source/Features/Tenancy/Core/Hosting/TenantHostResolver.cs` | 55 | 0 |
| `src/backend/Source/Middleware/CodedErrorResponse.cs` | 50 | 0 |
| `src/backend/Source/Middleware/TenantHostSessionMiddleware.cs` | 61 | 0 |
| `src/backend/Tests/Fakes/TenantHostingOverride.cs` | 48 | 0 |
| `src/backend/Tests/Features/Tenancy/Core/Hosting/TenantHostMapTests.cs` | 109 | 0 |
| `src/backend/Tests/Features/Tenancy/Core/Hosting/TenantHostingTests.cs` | 391 | 0 |

## The brief this was built from

Depends-on: 01-same-origin-api-and-dev-proxy

`acme.web.com` becomes tenant `acme` and `app.web.com` the platform host. The host chooses, it never grants: the session stays the only authority, and membership is still checked at sign-in. The host decides which tenant a sign-in targets and which session a request may act with. This task adds the configuration, a pure host resolver in the Tenancy slice, the tenant identifier on the session, a middleware that refuses a session used on the wrong host, the host rules for sign-in and switch/exit, the hub's same-origin check, and revocation when a tenant's identifier changes. No database query is added to ordinary requests. An empty template turns all of it off, which is what the Testing host and bearer clients on unknown hosts rely on.

## Scope
- **Configuration.** `Web:DefaultDomain` is the platform origin (`http://app.localhost:3000` in `appsettings.json` and Development). New `Web:TenantOriginTemplate` (`http://{tenant}.localhost:3000` in `appsettings.json` and Development), validated on start with the other `Web` options: exactly one `{tenant}`, which is the first host label, and the substituted value parses as an absolute URI. Empty means resolution is off. The Testing host leaves it empty; that default lives in `Program.cs` because `appsettings.Testing.json` is not in source control. Check the CORS `AllowedDomains()` list still admits what development needs; the CORS policy itself does not change.
- **`ITenantHostResolver`** (`[AllowOutside]`) in `Features/Tenancy/Core/Hosting/`: reads `HttpContext.Request.Host` against the two settings, pure parsing with no database access, returns `None` (off, or unknown host) / `Platform` / `Tenant(identifier)` (the label only, compared case-insensitively). It also provides `TenantUrl(identifier)` and `PlatformUrl()`. Unit tests cover the parsing (ports, case, nested labels, a host that only resembles the template, the platform label).
- **`ITenantService.FindByIdentifierAsync`** with `AcrossAllTenants`: the `IdentifierNormalized` query moved out of `TokenEndpoint.ResolveNamedTenantAsync`, which now calls it. No cache.
- `Program.cs` adds `ForwardedHeaders.XForwardedHost`, still trusted only from known proxies, or any proxy under `ForwardedHeaders:TrustAllProxies`.
- **Session.** `SessionRecord` gains `TenantIdentifier`; `ISessionIssuer` fills it wherever it creates a session (sign-in, refresh, switch/exit), from reads those paths already make; `SessionClaims.Project` exposes it as a `tenant_identifier` claim.
- **New `TenantHostSessionMiddleware`**, straight after `SessionStoreUnavailableMiddleware` so it covers endpoints and the notification hub. For an authenticated request: on `Tenant(x)` the `tenant_identifier` claim must equal `x`; on `Platform` the session must have no tenant; otherwise 403 `tenantHostMismatch`, localized like any coded error. `None` skips. A string comparison over the projected claims, no further reads.
- **Sign-in (`TokenEndpoint`).** Tenant host: the tenant is the host's, through the existing named-tenant path with its `tenantNotFound` / `notTenantMember` / `tenantSuspended` checks, platform accounts included; a request `TenantIdentifier` naming a different tenant is refused with `tenantHostMismatch`. Platform host: platform accounts sign in with no tenant; ordinary accounts are refused with new `tenantHostRequired`. `None`: unchanged.
- **`POST /tenants/switch` and `/tenants/exit`** answer `tenantHostMismatch` on a resolved host; unchanged under `None`.
- **Hub Origin check** no longer uses the fixed `WebSocketOptions.AllowedOrigins` list: an upgrade's `Origin` must equal the request's own scheme and host, or be one of `AllowedDomains()`; no `Origin` still passes.
- **Changing a tenant's `Identifier`** (`TenantUpdateEndpoint`) ends every session in that tenant through `ISessionRevocationService`, after commit, exactly as suspension does; an update that leaves the identifier unchanged revokes nothing.
- New `ErrorCodes.TenantHostMismatch` and `ErrorCodes.TenantHostRequired` with `error.server.*` messages, English and translated, in every resource file.
- **Tests**, in their own `[Collection("TenantHosting")]`, enabling the template through the fixture's configuration (the shared `App` keeps it empty — find the cleanest way, e.g. a dedicated fixture or resolver options override, without changing the behaviour of every other class) and sending `Host` headers: sign-in on a tenant host by a member and by a non-member; a suspended tenant; an unknown host; the platform host with a platform account and with an ordinary account; a `TenantIdentifier` that disagrees with the host; a session used on another tenant's host and on the platform host answering 403 `tenantHostMismatch` on an endpoint and on the hub upgrade; a hub upgrade from a foreign `Origin` refused and from the same origin accepted; switch/exit refused on a resolved host; renaming an identifier ending the tenant's sessions (old access token 401, refresh refused) and an unchanged identifier ending none; `tenant_identifier` present in the session after sign-in, refresh and switch.
- **Docs.** `CLAUDE.md` Auth section: how sign-in picks the tenant (host, then `None` behaviour), the host rule and `TenantHostSessionMiddleware`, the new revocation table row ("A tenant's identifier changed → every session in that tenant"), the new error codes, the `tenant_identifier` claim, the hub's origin rule, `XForwardedHost`, and the `Web:TenantOriginTemplate` startup guard; mirror whatever of that `tool/EasyForNetTool/new-project-claude.md` carries. The `multi-tenancy` skill: the host rule, the resolver, and that switching tenant on the web is navigation to another host (bearer clients keep switch/exit) — kept generic.

## Out of scope — do not touch
- `GET /tenants/host`, `GET /tenants/tls-allowed`, `GetInfoEndpoint`'s `url`/`platformUrl`, sign-up's host rule and links, `ResendVerifyEmailEndpoint` / `ForgetPasswordEndpoint` links, anonymous localization overrides, reserved identifiers — all `03-tenant-host-endpoints-and-links`.
- Permissions, `SessionGrants`, plan gating — the host never grants.
- The CORS policy.
- Anything under `src/frontend/web`, `scripts/`, `docker/`, compose files — owned by `01-same-origin-api-and-dev-proxy`, `04-web-workspaces-by-host` and `05-subdomain-deployment`.

## Done when
- `npm run verify -- --autostart` passes, and the existing backend suite is green with the template empty (Testing).
- The `TenantHosting` tests above pass.
- Under `None` (an unknown host, or the template empty), sign-in with `TenantIdentifier`, switch and exit behave exactly as before; a bearer client is unaffected.
- Standing promises still hold: an access change still revokes the sessions in the existing revocation table; a store outage still answers 503 `sessionStoreUnavailable`, never 401 or 403; the hub upgrade still answers 401 exactly when an HTTP request with the same credential would; a coded error is still served in the request's culture.
- By hand with `npm run dev`: signing in at `http://default.localhost:3000/signin` as `tenantadmin` lands in the default tenant (whatever the tenant field says if it names `default` or nothing); the same browser's cookie from `default.localhost` is not usable on another host; signing in at `http://app.localhost:3000` as `admin` works and as `tenantadmin` is refused with the `tenantHostRequired` message.
