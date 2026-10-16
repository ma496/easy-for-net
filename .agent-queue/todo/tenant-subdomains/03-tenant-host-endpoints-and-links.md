Expose the host's workspace to anonymous callers and build every link from the tenant's origin
Depends-on: 02-tenant-host-resolution-and-session-binding

With the API resolving hosts, the anonymous surface needs to follow: the web app must be able to ask which workspace a host is, Caddy must be able to ask whether a host deserves a certificate, an anonymous visitor on a tenant host must see that tenant's text overrides, sign-up must happen on the platform host and send its verification link to the new tenant's origin, emails must link to the host they came from, and identifiers that would collide with infrastructure hosts must be refused. Every lookup here goes through `ITenantService.FindByIdentifierAsync` and `ITenantHostResolver` from the previous task; only these anonymous endpoints look a tenant up by identifier.

## Scope
- **New anonymous `GET /tenants/host`:** `{ kind: "platform", platformUrl }` on the platform host or under `None`; `{ kind: "tenant", name, identifier, status, platformUrl }` on a tenant host whose tenant exists and is not deleted (suspended tenants are returned with their status); 404 `tenantNotFound` otherwise.
- **New anonymous `GET /tenants/tls-allowed?domain=`:** 200 only when `domain` resolves (through the same parsing as `ITenantHostResolver`, applied to the given name rather than the request host) to the platform host or to an existing, non-deleted tenant; 404 otherwise, including when resolution is off. Rate-limit behaviour matches the other anonymous endpoints.
- **`GetInfoEndpoint`:** adds `url` (`TenantUrl(identifier)`) to every tenant it lists and returns `platformUrl`, so the web app never bakes in a domain. Fields are null/absent only when resolution is off — decide and document which, consistently with the DTO.
- **Sign-up:** allowed on the platform host or under `None`; on a tenant host it answers `tenantHostMismatch`. Its verification email links to the **new tenant's** origin.
- **`ResendVerifyEmailEndpoint` and `ForgetPasswordEndpoint`:** build their links from the request's own resolved host (tenant URL or platform URL), falling back to `Web:DefaultDomain` under `None`. Any other email link built from `Web:DefaultDomain` is routed through `TenantUrl` / `PlatformUrl` the same way.
- **Anonymous `GET /localization/resources/{culture}` on a tenant host** serves that tenant's overrides (texts and language settings) through an explicit tenant argument to `ILocalizationService`; no general tenant scope is opened for anonymous requests; an unknown tenant host falls back to the platform; authenticated callers are unchanged.
- **Reserved identifiers:** `TenantValidationRules.TenantIdentifier()` refuses `app`, `www`, `api`, `admin`, `mail`, `smtp`, `static`, `cdn`, `status`, `help`, `docs`, `hangfire` and the platform host's own label (read from `Web:DefaultDomain`), case-insensitively, with new `ErrorCodes.TenantIdentifierReserved`; this reaches sign-up, tenant create and tenant update through the one rule.
- `error.server.tenantIdentifierReserved` and any other new code, English and translated, in every resource file.
- **Tests** in the `TenantHosting` collection from the previous task: `GET /tenants/host` for each kind (platform, tenant, suspended tenant, unknown → 404, `None`); `tls-allowed` for the platform host, an existing tenant, a deleted tenant, an unknown label and an unrelated domain; anonymous `resources/{culture}` returning the host tenant's override and the platform's on the platform host; email links pointing at the right origin via `RecordingEmailTransport` (sign-up → new tenant origin, forget-password and resend-verify → the request's host, `None` → `Web:DefaultDomain`); sign-up refused on a tenant host; reserved identifiers refused at sign-up, tenant create and tenant update (outside the collection is fine for these, since they need no host); `GetInfo` carrying `url` and `platformUrl`.
- **Docs.** `CLAUDE.md`: the Localization paragraph (anonymous caller on a tenant host sees that tenant's overrides), the Auth section's sign-up/host lines and the reserved-identifier rule; mirror in `tool/EasyForNetTool/new-project-claude.md` where it carries that text. The `multi-tenancy` and `localization` skills where they describe what an anonymous caller sees — generic.

## Out of scope — do not touch
- The resolver, `TenantHostSessionMiddleware`, sign-in host rules, switch/exit, the hub origin check, session `TenantIdentifier`, identifier-rename revocation — owned by `02-tenant-host-resolution-and-session-binding`; reuse, do not rework.
- Anything under `src/frontend/web` — `04-web-workspaces-by-host` consumes these endpoints.
- `docker/Caddyfile` and the compose files — `05-subdomain-deployment` wires `tls-allowed` into Caddy.
- Permissions, `SessionGrants`, plan gating; the CORS policy.

## Done when
- `npm run verify -- --autostart` passes, and the existing backend suite is green with the template empty.
- The tests above pass.
- `Tests/Features/Localization/Core/LocalizationResourceStoreTests` stays green: every resource file has the new keys, none empty or equal to its key.
- Standing promises still hold: an anonymous `resources/{culture}` with no host resolution still sees platform overrides only; an authenticated caller still sees its acting tenant's; a coded error is served in the request's culture.
- By hand with `npm run dev`: `curl -H "Host: default.localhost:3000" http://localhost:5000/api/tenants/host` returns the default tenant; `.../tenants/tls-allowed?domain=nope.localhost` is 404; creating a tenant named `app` is refused with the reserved-name message.
