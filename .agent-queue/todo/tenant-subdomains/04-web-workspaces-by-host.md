Choose the workspace by address in the web app and remove the in-place tenant switch
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
