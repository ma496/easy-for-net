# Choose the workspace by address in the web app and remove the in-place tenant switch

| | |
|---|---|
| **Task** | `tenant-subdomains/04-web-workspaces-by-host` |
| **Landed** | 2026-10-16 |
| **Commit** | the one whose message carries `Task: tenant-subdomains/04-web-workspaces-by-host` |
| **Verification** | `npm run verify` — the static gate, plus whichever live checks the diff demanded |

## What changed

| File | + | − |
|------|---|---|
| `.claude/skills/frontend-tests/SKILL.md` | 8 | 5 |
| `.claude/skills/localization/SKILL.md` | 3 | 3 |
| `.claude/skills/multi-tenancy/SKILL.md` | 8 | 6 |
| `.claude/skills/new-project/SKILL.md` | 4 | 3 |
| `.claude/skills/notifications/SKILL.md` | 1 | 2 |
| `.claude/skills/redux-state/SKILL.md` | 5 | 9 |
| `.claude/skills/rtk-query-api/SKILL.md` | 4 | 5 |
| `.claude/skills/ui-component/SKILL.md` | 2 | 2 |
| `CLAUDE.md` | 4 | 2 |
| `src/backend/Source/Features/Localization/Core/Resources/ar.json` | 41 | 22 |
| `src/backend/Source/Features/Localization/Core/Resources/en.json` | 41 | 22 |
| `src/backend/Source/Features/Localization/Core/Resources/es.json` | 41 | 22 |
| `src/backend/Source/Features/Localization/Core/Resources/fr.json` | 41 | 22 |
| `src/backend/Source/Features/Localization/Core/Resources/hi.json` | 41 | 22 |
| `src/backend/Source/Features/Localization/Core/Resources/ru.json` | 41 | 22 |
| `src/backend/Source/Features/Localization/Core/Resources/ur.json` | 41 | 22 |
| `src/backend/Source/Features/Localization/Core/Resources/zh.json` | 41 | 22 |
| `src/backend/Source/Features/Tenancy/Core/Hosting/TenantHostMap.cs` | 7 | 0 |
| `src/backend/Source/Features/Tenancy/Core/Hosting/TenantHostResolver.cs` | 10 | 0 |
| `src/backend/Source/Features/Tenancy/Endpoints/Tenants/TenantHostGetEndpoint.cs` | 18 | 4 |
| `src/backend/Tests/Fakes/TenantHostingOverride.cs` | 6 | 1 |
| `src/backend/Tests/Features/Tenancy/Core/Hosting/TenantHostLinksTests.cs` | 5 | 0 |
| `src/backend/Tests/Features/Tenancy/Core/Hosting/TenantHostMapTests.cs` | 3 | 0 |
| `src/frontend/web/App.tsx` | 11 | 13 |
| `src/frontend/web/app/[lang]/(auth)/select-tenant/_components/select-tenant-view.test.ts` | 43 | 39 |
| `src/frontend/web/app/[lang]/(auth)/select-tenant/_components/select-tenant-view.tsx` | 73 | 48 |
| `src/frontend/web/app/[lang]/(auth)/select-tenant/page.tsx` | 2 | 2 |
| `src/frontend/web/app/[lang]/(auth)/signin/_components/signin-form.tsx` | 197 | 67 |
| `src/frontend/web/app/[lang]/(auth)/signin/page.tsx` | 8 | 8 |
| `src/frontend/web/app/[lang]/(auth)/signup/_components/signup-form.tsx` | 252 | 64 |
| `src/frontend/web/app/[lang]/(auth)/signup/page.tsx` | 8 | 8 |
| `src/frontend/web/app/[lang]/admin/(tenancy)/tenants/_components/tenant-table.tsx` | 16 | 19 |
| `src/frontend/web/components/custom/index.ts` | 1 | 1 |
| `src/frontend/web/components/custom/nav-user.tsx` | 53 | 5 |
| `src/frontend/web/components/custom/tenant-switcher.tsx` | 0 | 167 |
| `src/frontend/web/components/layouts/header.tsx` | 3 | 4 |
| `src/frontend/web/components/layouts/index.ts` | 0 | 1 |
| `src/frontend/web/components/layouts/main-content.tsx` | 0 | 2 |
| `src/frontend/web/components/layouts/tenant-change-notice.tsx` | 0 | 26 |
| `src/frontend/web/components/ui/data-table/row-actions.tsx` | 11 | 3 |
| `src/frontend/web/components/ui/form/form-input.tsx` | 8 | 2 |
| `src/frontend/web/hooks/index.ts` | 0 | 1 |
| `src/frontend/web/hooks/use-notification-hub.ts` | 3 | 3 |
| `src/frontend/web/hooks/use-tenant-switch.ts` | 0 | 75 |
| `src/frontend/web/i18n/tenant-screens.test.ts` | 51 | 7 |
| `src/frontend/web/lib/server/api-fetch.test.ts` | 29 | 1 |
| `src/frontend/web/lib/server/api-fetch.ts` | 37 | 11 |
| `src/frontend/web/lib/utils/authentication-and-authorization.test.ts` | 4 | 1 |
| `src/frontend/web/lib/utils/index.ts` | 2 | 2 |
| `src/frontend/web/lib/utils/tenant-routing.test.ts` | 23 | 43 |
| `src/frontend/web/lib/utils/tenant-routing.ts` | 16 | 35 |
| `src/frontend/web/store/api/identity/account/account-dtos.ts` | 8 | 4 |
| `src/frontend/web/store/api/settings/settings/settings-api.ts` | 2 | 2 |
| `src/frontend/web/store/api/tenancy/index.ts` | 4 | 6 |
| `src/frontend/web/store/api/tenancy/tenants/tenants-api.ts` | 15 | 27 |
| `src/frontend/web/store/api/tenancy/tenants/tenants-dtos.ts` | 18 | 28 |
| `src/frontend/web/store/slices/authSlice.test.ts` | 3 | 1 |
| `src/frontend/web/store/slices/authSlice.ts` | 1 | 1 |
| `src/frontend/web/store/tenant-cache.test.ts` | 62 | 90 |
| `src/frontend/web/store/tenant-cache.ts` | 0 | 52 |
| `tool/EasyForNetTool/new-project-claude.md` | 4 | 2 |
| `src/frontend/web/components/custom/workspace-link.tsx` | 78 | 0 |
| `src/frontend/web/lib/server/host-workspace.test.ts` | 43 | 0 |
| `src/frontend/web/lib/server/host-workspace.ts` | 74 | 0 |
| `src/frontend/web/lib/utils/workspace-url.test.ts` | 46 | 0 |
| `src/frontend/web/lib/utils/workspace-url.ts` | 31 | 0 |

## The brief this was built from

Depends-on: 01-same-origin-api-and-dev-proxy, 03-tenant-host-endpoints-and-links

The API now picks the tenant from the host, and each host keeps its own session. The web app still asks for a tenant at sign-in, shows a tenant switcher in the header, and switches tenant in place through `/tenants/switch` / `exit`, which now answer `tenantHostMismatch` on a resolved host. This task makes the web app match: sign-in names the workspace the host is, the header names no tenant, other workspaces are plain links to their own address (from `tenant.url` / `platformUrl`, never a baked-in domain), sign-up happens on the platform host, and the in-place switch machinery is deleted.

## Scope
- **Sign-in form:** remove the tenant field, its validation and its value. The heading names the workspace from `GET /tenants/host` ("Sign in to Acme"); the platform host gets a platform heading. An unknown host (404 `tenantNotFound`) shows "workspace not found" with a link to `platformUrl`; a `tenantHostRequired` refusal is explained the same way, with that link. Add the RTK Query endpoint and DTO for `GET /tenants/host`, and `url` / `platformUrl` to the get-info DTOs.
- **Header:** remove `TenantSwitcher`; nothing in the header names the current tenant.
- **Other workspaces:** the user menu lists the account's other tenants as plain links to each `tenant.url`, opening in a new tab; platform accounts also get a link to `platformUrl`. `select-tenant-view.tsx` becomes the same list of links. The tenants table's "enter" action becomes a link to the tenant's `url`.
- **Delete the in-place switch path:** `hooks/use-tenant-switch.ts`, `components/custom/tenant-switcher.tsx`, the `tenantSwitch` / `tenantExit` mutations, `leaveForTenantChange` / `takeTenantChangeNotice` in `store/tenant-cache.ts`, and the post-sign-in chooser landing in `lib/utils/tenant-routing.ts`. Remove anything left unused by these deletions.
- **Rewrite the source-chain tests** (`store/tenant-cache.test.ts`, `select-tenant-view.test.ts`, `i18n/tenant-screens.test.ts`, `tenant-routing.test.ts`) so they pin the new rule: nothing in the web app calls `/tenants/switch` or `/tenants/exit`, and tenant links come from `tenant.url`. Do not weaken unrelated assertions in them.
- **Sign-up form:** shown on the platform host; on a tenant host it shows a link to the platform's sign-up instead. The identifier field previews the resulting address (from the template the API exposes through `GET /tenants/host` / get-info — add a field to `03`'s response only if the address cannot be derived otherwise, and then add its test too) and shows the reserved-name error. The success view links to the new workspace's sign-in page.
- **Translations:** new keys in every backend resource file (`Features/Localization/Core/Resources/*.json`, with translations); remove keys nothing uses any more (the tenant field, the switcher, the switch notice). Every translator call keeps its English default after `||`.
- **Design first:** `ui-ux-reviewer` designs the sign-in heading / workspace-not-found state, the user menu's workspace list and the sign-up address preview before they are built (dark mode, RTL, narrow screens).
- **Docs.** `CLAUDE.md` frontend section: the browser reaches the API same-origin at `/api`, and changing tenant is navigating to that tenant's address (anything there still describing `router.refresh()` after a tenant switch is updated). Mirror in `tool/EasyForNetTool/new-project-claude.md`. Skills that describe the in-place switch or the tenant-change reset sequence (`frontend-page`, `redux-state`, `rtk-query-api`, `frontend-tests`, the web part of `multi-tenancy`) and `new-project` (sign in at `http://default.localhost:3000` with no tenant field; platform at `http://app.localhost:3000`) — kept generic.

## Out of scope — do not touch
- `lib/notifications/hub-url.ts` and `proxy.ts` — unchanged by this plan.
- `config/environment.ts`, the server-side forwarded-host helper, `.env.example`, `Dockerfile`, `scripts/dev*.mjs` — owned by `01-same-origin-api-and-dev-proxy`.
- Backend endpoints and rules (`src/backend/Source/**` other than the resource files' text keys) — owned by `02-tenant-host-resolution-and-session-binding` and `03-tenant-host-endpoints-and-links`; do not change error codes or their messages.
- Deployment files — `05-subdomain-deployment`.

## Done when
- `npm run verify -- --autostart` passes; in `src/frontend/web`, `npm run lint`, `npx tsc --noEmit` and `npm run test` pass with the rewritten source-chain tests, and `i18n/default-texts.test.ts` and `LocalizationResourceStoreTests` stay green.
- A search of `src/frontend/web` finds no call to `/tenants/switch` or `/tenants/exit`, and no hard-coded domain in a workspace link.
- By hand with `npm run dev`:
  - `http://default.localhost:3000/signin` has no tenant field, says which workspace it is, and signs `tenantadmin` in to the default tenant.
  - `http://nope.localhost:3000/signin` shows "workspace not found" with a working link to the platform.
  - A second tenant created by sign-up from `http://app.localhost:3000` can be signed in to in another tab from the success view's link; both tabs stay signed in to their own tenant; signing out of one leaves the other signed in; each receives its own notifications.
  - The header shows no tenant, and the user menu's workspace links open the other host in a new tab.
  - Signing up with the identifier `app` is refused with the reserved-name message, and the identifier field previews the address as it is typed.
  - The pages look right in dark mode and in an RTL language.
