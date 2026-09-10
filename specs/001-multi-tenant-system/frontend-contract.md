# Frontend contract - Multi-tenant system

Spec: `specs/001-multi-tenant-system/spec.md` - Plan: `specs/001-multi-tenant-system/plan.md`

Scope: everything under `src/frontend/web`. This document fixes the RTK Query surface, the DTO
names, the cache-tag decisions, the routes and components, the permission mirror, the route guards,
the navigation and search entries, and every translation key. It contains no code.

Governing skills: `rtk-query-api`, `frontend-crud`, `frontend-page`, `ui-component`,
`localization`, `permissions`, `coding-conventions`.

---

## 1. Framing decisions this contract inherits

| From | Decision | Consequence here |
| --- | --- | --- |
| Plan D19 | No new API client, no new Redux slice, no browser storage for the active tenant | Tenant endpoints inject into the existing `appApi`; `authSlice` widens; nothing is written to `localStorage`/`sessionStorage`/a cookie of our own (AC-124, AC-125) |
| Plan D19 | The active tenant is re-read from `/account/get-info` | `App.tsx` and `SigninForm` already call it, so AC-024, AC-073, AC-123, AC-127 and AC-142 need no extra round trip |
| Plan, "Tenant addressing" (AC-138) | The active tenant travels in the session, never in a request value or a URL segment | The locale-prefixed route tree keeps its shape; **no route carries a tenant segment**, and no request DTO carries the *active* tenant. `tenantId` appears only where a specific tenant is being *administered* (member endpoints) or *selected* (switch) |
| Plan D15 | Suspend and reactivate are separately gated endpoints | Two mutations, two `isAllowed` checks, two row actions - never one "set status" control |
| Spec AC-093..AC-096, AC-110..AC-113 | User and role administration change rows, not routes or payloads | The existing users, roles, change-permissions and notification screens are **unchanged** (see §14) |

### Deviation from plan §6, recorded

Plan §6 places the new API slice at `store/api/identity/tenants/`. This contract places it at
`store/api/tenancy/tenants/` instead, because the `rtk-query-api` skill states that `<feature>` and
`<area>` **mirror the backend**, and plan D2 makes tenancy its own backend slice
(`Backend.Features.Tenancy`). Filing tenant endpoints under `identity` would be the only place in
`store/api/` where the web feature folder does not name the backend feature that serves it.
`store/api/identity/` still changes, but only for the `/account/get-info` DTO additions, which
genuinely belong to Identity.

---

## 2. DTO files and interfaces

### 2.1 New: `src/frontend/web/store/api/tenancy/tenants/tenants-dtos.ts`

Every interface mirrors the C# request/response class of the same name (plan §2), camelCased, with
`Guid` -> `string` and `DateTime` -> `string`, extending `BaseDto<string>`, `RequestBase`,
`GenericAuditableDto<string>`, `ListRequestDto<string>` or `ListDto<T>` from `@/store/api`. Each
carries a one-line JSDoc (`coding-conventions`).

| Interface | Extends | Members | Serves |
| --- | --- | --- | --- |
| `TenantListRequest` | `ListRequestDto<string>`, `RequestBase` | `status?: TenantStatus` | AC-061, AC-062, AC-064, AC-065, AC-066 |
| `TenantListResponse` | `ListDto<TenantListDto>` | - | AC-061 |
| `TenantListDto` | `GenericAuditableDto<string>` | `systemCreated: boolean`, `name: string`, `identifier: string`, `identifierNormalized: string`, `status: TenantStatus` | AC-001, AC-011, AC-012, AC-071 |
| `TenantGetRequest` | `BaseDto<string>`, `RequestBase` | - | AC-071 |
| `TenantGetResponse` | `GenericAuditableDto<string>` | `systemCreated`, `name`, `identifier`, `identifierNormalized`, `status: TenantStatus` | AC-005, AC-012 |
| `TenantCreateRequest` | `RequestBase` | `name: string`, `identifier: string` | AC-002, AC-100, AC-101 |
| `TenantCreateResponse` | `BaseDto<string>` | `name`, `identifier`, `identifierNormalized`, `status: TenantStatus` | AC-002 |
| `TenantUpdateRequest` | `BaseDto<string>`, `RequestBase` | `name: string`, `identifier: string` | AC-005 |
| `TenantUpdateResponse` | `BaseDto<string>` | `name`, `identifier`, `identifierNormalized`, `status: TenantStatus` | AC-005 |
| `TenantSuspendRequest` | `BaseDto<string>`, `RequestBase` | - | AC-006 |
| `TenantSuspendResponse` | `BaseDto<string>` | `status: TenantStatus` | AC-006 |
| `TenantReactivateRequest` | `BaseDto<string>`, `RequestBase` | - | AC-008 |
| `TenantReactivateResponse` | `BaseDto<string>` | `status: TenantStatus` | AC-008 |
| `TenantDeleteRequest` | `BaseDto<string>`, `RequestBase` | - | AC-009 |
| `TenantDeleteResponse` | `BaseDto<string>` | `success: boolean`, `message: string` | AC-009 (mirrors `UserDeleteResponse`) |
| `TenantMemberListRequest` | `ListRequestDto<string>`, `RequestBase` | `tenantId: string` (path segment, see §3) | AC-061, AC-062, AC-064, AC-072 |
| `TenantMemberListResponse` | `ListDto<TenantMemberListDto>` | - | AC-072 |
| `TenantMemberListDto` | `GenericAuditableDto<string>` | `userId: string`, `username: string`, `email: string`, `firstName?: string`, `lastName?: string`, `roles: TenantMemberRoleDto[]` | AC-013, AC-072, AC-078 |
| `TenantMemberRoleDto` | `BaseDto<string>` | `name: string` | AC-072 (mirrors `UserRoleDto`) |
| `TenantMemberAddRequest` | `RequestBase` | `tenantId: string`, `userId: string`, `roles: string[]` | AC-014, AC-106 |
| `TenantMemberAddResponse` | `BaseDto<string>` | `userId: string`, `roles: string[]` | AC-014 |
| `TenantMemberUpdateRolesRequest` | `RequestBase` | `tenantId: string`, `userId: string`, `roles: string[]` | AC-017, AC-081 |
| `TenantMemberUpdateRolesResponse` | `BaseDto<string>` | `userId: string`, `roles: string[]` | AC-017 |
| `TenantMemberRemoveRequest` | `RequestBase` | `tenantId: string`, `userId: string` | AC-018, AC-019 |
| `TenantMemberRemoveResponse` | `BaseDto<string>` | `success: boolean`, `message: string` | AC-018 |
| `TenantOnboardRequest` | `RequestBase` | `name: string`, `identifier: string` | AC-133, AC-134 |
| `TenantOnboardResponse` | `BaseDto<string>` | `name`, `identifier`, `status: TenantStatus` | AC-133 |
| `TenantSwitchRequest` | `RequestBase` | `tenantId: string` | AC-025, AC-139 |
| `TenantSwitchResponse` | - | `tenantId: string`, `name: string`, `identifier: string` | AC-025, AC-108 |

`TenantMemberListDto.id` is the **membership** id; `userId` is the account. Row actions address the
member by `userId`, matching the request DTOs above.

### 2.2 New: `src/frontend/web/store/api/tenancy/enums.ts`

`TenantStatus { Active = 'Active', Suspended = 'Suspended' }` - string-valued, mirroring
`NotificationType` in `store/api/notifications/enums.ts` (the backend serialises that enum as
strings). Serves AC-001, AC-065, AC-071.

### 2.3 New: `src/frontend/web/store/api/tenancy/index.ts`

Feature barrel, shaped like `store/api/identity/index.ts` and `store/api/notifications/index.ts`:
re-exports `tenantsApi`, every generated hook (§3), `TenantStatus`, and every DTO type as
`export type`. Components import from `@/store/api/tenancy`, never from the deep path
(`coding-conventions`).

`store/api/index.ts` is **not** changed - it exports only the shared base DTO types, and feature
barrels are imported directly today.

### 2.4 Edited: `src/frontend/web/store/api/identity/account/account-dtos.ts`

| Interface | Change | Serves |
| --- | --- | --- |
| `GetUserInfoResponse` | add `activeTenant?: GetUserInfoTenant` and `tenants: GetUserInfoTenant[]` | AC-024, AC-073, AC-123, AC-127, AC-140, AC-142 |
| `GetUserInfoTenant` | **new**: `id: string`, `name: string`, `identifier: string` | AC-024, AC-029 |

`roles[].permissions[]` keeps its shape: the backend now returns the caller's roles **in the active
tenant** (AC-027, AC-108), so `isAllowed` needs no signature change. `activeTenant` is absent when
the user holds memberships in more than one tenant and has not chosen (AC-140), and `tenants` is
empty for an account with no usable membership (AC-050, AC-122).

`store/api/identity/index.ts` gains `GetUserInfoTenant` to its `export type` list.
`store/api/identity/account/account-api.ts` is **unchanged** - `getUserInfo` already exists, and the
switch/onboard mutations live in the tenancy slice.

### 2.5 Edited: `src/frontend/web/store/api/file-management/files/files-dtos.ts`

`FileUploadRequest` gains a field declaring that an upload is account-owned rather than
tenant-scoped, so a profile image can be set by a user acting in any tenant or in none (AC-097,
AC-051, AC-099). OQ-1 is answered: the wire field is a boolean on the existing upload request. The interface
is not edited and the profile avatar keeps its current call shape.

---

## 3. RTK Query endpoints

New file `src/frontend/web/store/api/tenancy/tenants/tenants-api.ts`. It attaches to the shared
`appApi` - **`createApi` is never called again**:
`appApi.enhanceEndpoints({ addTagTypes: ['Tenants', 'TenantMembers', 'Users'] }).injectEndpoints({ overrideExisting: false, endpoints })`.

Endpoint names match the backend endpoint names (plan §2), so the generated hooks come out as
`useTenantListQuery`, `useTenantCreateMutation`, and so on.

| Endpoint | Kind | Method + URL | Request -> Response | Serves |
| --- | --- | --- | --- | --- |
| `tenantList` | query | `GET /tenants` with params `page, pageSize, sortField, sortDirection, search, all, includeIds, status` | `TenantListRequest` -> `TenantListResponse` | AC-061..AC-066, AC-071, AC-046 |
| `tenantGet` | query | `GET /tenants/{id}` | `TenantGetRequest` -> `TenantGetResponse` | AC-071 |
| `tenantCreate` | mutation | `POST /tenants` | `TenantCreateRequest` -> `TenantCreateResponse` | AC-002, AC-003, AC-004 |
| `tenantUpdate` | mutation | `PUT /tenants/{id}`, id stripped from the body | `TenantUpdateRequest` -> `TenantUpdateResponse` | AC-005 |
| `tenantSuspend` | mutation | `POST /tenants/{id}/suspend` | `TenantSuspendRequest` -> `TenantSuspendResponse` | AC-006 |
| `tenantReactivate` | mutation | `POST /tenants/{id}/reactivate` | `TenantReactivateRequest` -> `TenantReactivateResponse` | AC-008 |
| `tenantDelete` | mutation | `DELETE /tenants/{id}` | `TenantDeleteRequest` -> `TenantDeleteResponse` | AC-009 |
| `tenantMemberList` | query | `GET /tenants/{tenantId}/members` with the standard list params | `TenantMemberListRequest` -> `TenantMemberListResponse` | AC-061, AC-064, AC-072 |
| `tenantMemberAdd` | mutation | `POST /tenants/{tenantId}/members` | `TenantMemberAddRequest` -> `TenantMemberAddResponse` | AC-014, AC-015, AC-016 |
| `tenantMemberUpdateRoles` | mutation | `PUT /tenants/{tenantId}/members/{userId}` | `TenantMemberUpdateRolesRequest` -> `TenantMemberUpdateRolesResponse` | AC-017, AC-019, AC-081 |
| `tenantMemberRemove` | mutation | `DELETE /tenants/{tenantId}/members/{userId}` | `TenantMemberRemoveRequest` -> `TenantMemberRemoveResponse` | AC-018, AC-019 |
| `tenantOnboard` | mutation | `POST /tenants/onboard` | `TenantOnboardRequest` -> `TenantOnboardResponse` | AC-133..AC-137, AC-143 |
| `tenantSwitch` | mutation | `POST /tenants/switch` | `TenantSwitchRequest` -> `TenantSwitchResponse` | AC-025, AC-108, AC-139 |

Routes follow plan §2, which places all thirteen endpoints in `Endpoints/Tenants/` under
`TenantsGroup` (prefix `tenants`). `tenantId` is a **path segment** in the member endpoints and is
stripped from bodies and query strings by the `query` function, exactly as `userUpdate` strips `id`
today.

Exported hooks: `useTenantListQuery`, `useLazyTenantListQuery`, `useTenantGetQuery`,
`useLazyTenantGetQuery`, `useTenantCreateMutation`, `useTenantUpdateMutation`,
`useTenantSuspendMutation`, `useTenantReactivateMutation`, `useTenantDeleteMutation`,
`useTenantMemberListQuery`, `useLazyTenantMemberListQuery`, `useTenantMemberAddMutation`,
`useTenantMemberUpdateRolesMutation`, `useTenantMemberRemoveMutation`, `useTenantOnboardMutation`,
`useTenantSwitchMutation`.

---

## 4. Cache tag decisions

Two new tag types, `Tenants` and `TenantMembers` - the PascalCase plural of each area, declared on
this slice only. `Users` is re-declared in this slice's `addTagTypes` so member mutations can
invalidate it (re-declaring an existing tag type is additive and keeps the union type-safe).

**Endpoints that provide tags**

| Endpoint | `providesTags` | Why |
| --- | --- | --- |
| `tenantList` | `['Tenants', ...rows.map(r => ({ type: 'Tenants', id: r.id }))]` | the standard list pattern, so a row mutation refreshes both the page and the row |
| `tenantGet` | `[{ type: 'Tenants', id: arg.id }]` | row tag only |
| `tenantMemberList` | `['TenantMembers', ...rows.map(r => ({ type: 'TenantMembers', id: r.userId }))]` | keyed on `userId`, because every member mutation addresses the member by `userId` |

**Endpoints that invalidate tags**

| Endpoint | `invalidatesTags` | Why |
| --- | --- | --- |
| `tenantCreate` | `['Tenants']` | only the collection changed |
| `tenantUpdate` | `['Tenants', { type: 'Tenants', id: arg.id }]` | collection + row (AC-005) |
| `tenantSuspend` | `['Tenants', { type: 'Tenants', id: arg.id }]` | status shows in both the list and the detail (AC-006) |
| `tenantReactivate` | `['Tenants', { type: 'Tenants', id: arg.id }]` | AC-008 |
| `tenantDelete` | `['Tenants', { type: 'Tenants', id: arg.id }]` | AC-009 |
| `tenantMemberAdd` | `['TenantMembers', 'Users']` | the member list changed, and the tenant-scoped user list gains a row (AC-093, AC-096) |
| `tenantMemberUpdateRoles` | `['TenantMembers', { type: 'TenantMembers', id: arg.userId }, 'Users', { type: 'Users', id: arg.userId }]` | roles show on both surfaces (AC-017) |
| `tenantMemberRemove` | `['TenantMembers', { type: 'TenantMembers', id: arg.userId }, 'Users', { type: 'Users', id: arg.userId }]` | the account survives, its membership does not (AC-018, AC-093) |
| `tenantOnboard` | `['Tenants']` | the caller's tenant list changed (AC-133) |

**Endpoints that need no tags**

| Endpoint | Why |
| --- | --- |
| `tenantSwitch` | invalidation is the wrong tool: the whole cache must be dropped, not selectively refreshed. The handler dispatches `appApi.util.resetApiState()` instead (AC-028, AC-132). An `invalidatesTags` here would refetch previous-tenant queries during the switch - precisely what AC-028 forbids |
| `getUserInfo` (existing, `account-api.ts`) | session state lives in `authSlice` and the reauth flow, per the `rtk-query-api` skill; it is refetched explicitly after a switch or an onboard |
| `fileGet` / `fileUpload` / `fileDelete` (existing) | files are addressed by name and nothing lists them; cross-tenant blobs are dropped by `resetApiState` on switch (AC-058, AC-028) |
| `notificationGetUnreadCount` (existing) | polls on its own interval; tagging it would only add redundant refetches. AC-056 is served by `resetApiState` plus `setUnreadCount(0)` (§6.4) |

**Cache-reset rules (AC-028, AC-125, AC-132)**

1. On a successful `tenantSwitch`: `dispatch(appApi.util.resetApiState())` **before** refetching
   `getUserInfo`, then `dispatch(setUserInfo(fresh))`, then `dispatch(setUnreadCount(0))`.
2. On a successful `tenantOnboard`: the same sequence - onboarding makes the new tenant active
   (AC-133).
3. On sign-out: `dispatch(signout())` followed by `dispatch(appApi.util.resetApiState())`, so the
   next user on the same browser inherits neither the selection nor a cached row (AC-125).

---

## 5. Locale-prefixed routes, page components and client components

Every route is locale-prefixed: the default locale is served unprefixed and other locales carry
`/{lang}` (handled by `proxy.ts`). Paths below are written unprefixed, as `nav-items.ts`,
`auth-urls.ts` and `LocalizedLink` expect them.

| Route | Server page component (`page.tsx`, default export) | Client components (`_components/`, `'use client'`, named export) | Serves |
| --- | --- | --- | --- |
| `/admin/tenants/list` | `app/[lang]/admin/(tenancy)/tenants/list/page.tsx` - `Tenants`, resolves `page.tenants.list.title` via `getServerTranslation`, renders `<TenantTable />` inside `AdminPageContent` | `tenant-table.tsx` (`TenantTable`), `tenant-filter-panel.tsx` (`TenantFilterPanel`, exports `TenantFilters`), `tenant-filter-button.tsx` (`TenantFilterButton`) | AC-061..AC-066, AC-071, AC-046, AC-141 |
| `/admin/tenants/create` | `.../tenants/create/page.tsx` - `TenantCreate`, title `page.tenants.create.title`, `innerClassName="max-w-187.5"` | `tenant-create-form.tsx` (`TenantCreateForm`) | AC-002, AC-003, AC-004, AC-100, AC-101, AC-071 |
| `/admin/tenants/update/{id}` | `.../tenants/update/[id]/page.tsx` - `TenantUpdate`, destructures `{ lang, id }`, title `page.tenants.update.title`, `innerClassName="max-w-155"` | `tenant-update-form.tsx` (`TenantUpdateForm`, prop `tenantId`) | AC-005, AC-011, AC-071 |
| `/admin/tenants/members/{id}` | `.../tenants/members/[id]/page.tsx` - `TenantMembers`, destructures `{ lang, id }`, title `page.tenants.members.title` | `tenant-member-table.tsx` (`TenantMemberTable`, prop `tenantId`), `tenant-member-add-modal.tsx` (`TenantMemberAddModal`), `tenant-member-roles-modal.tsx` (`TenantMemberRolesModal`) | AC-013, AC-014, AC-017, AC-018, AC-019, AC-072 |
| `/select-tenant` | `app/[lang]/(auth)/select-tenant/page.tsx` - `SelectTenant`, with `generateMetadata` using `page.selectTenant.title` (the `(auth)` group's existing pattern, as `unauthorized` does) | `select-tenant-view.tsx` (`SelectTenantView`) | AC-024, AC-025, AC-029, AC-070, AC-127, AC-140, AC-142 |
| `/no-tenant` | `app/[lang]/(auth)/no-tenant/page.tsx` - `NoTenant`, with `generateMetadata` using `page.noTenant.title` | `no-tenant-view.tsx` (`NoTenantView`), `tenant-onboard-form.tsx` (`TenantOnboardForm`) | AC-050, AC-122, AC-126, AC-133, AC-134, AC-143 |

`(tenancy)` is a route group, so it adds no URL segment - it mirrors `admin/(identity)`.
`select-tenant` and `no-tenant` sit in `(auth)`, not `admin/`, because they must render for a user
who has no active tenant and therefore may not open any tenant-scoped screen (AC-126, AC-142).

**Behaviour the client components own**

- `TenantTable` - `useTableUrlState({ filters: { status: parseAsStringEnum(['Active','Suspended']).withOptions({ clearOnDefault: true, history: 'push' }) } })`,
  `useTenantListQuery`, `DataTableProvider` + `DataTableToolbar` + `DataTable` +
  `DataTablePagination`, export through `useLazyTenantListQuery({ all: true })` + `exportData`.
  Columns: `table.columns.name`, `table.columns.identifier`, `table.columns.status` (a `Badge`
  rendering `page.tenants.status.*`), `table.columns.updated`, and a display `actions` column.
  Sortable columns are limited to the fields the backend list validator whitelists (AC-062,
  AC-063). Row actions, each gated: update (`Allow.Tenant_Update`), members
  (`Allow.TenantMember_View`), suspend (`Allow.Tenant_Suspend`, shown while `status === Active`),
  reactivate (`Allow.Tenant_Reactivate`, shown while `status === Suspended`), delete
  (`Allow.Tenant_Delete`). Every mutating action is hidden for a row with `systemCreated === true`
  (AC-011); a `page.tenants.systemCreated` badge says why.
- `TenantCreateForm` / `TenantUpdateForm` - Formik + Yup built from `t`, mirroring the API
  validators so the user sees the rule before the round trip (AC-100, AC-101, spec "Naming and
  normalization"): `name` required, 2..100 after trim; `identifier` required, 3..50, matched
  against the lower-case letters/digits/single-interior-hyphen rule with
  `validation.tenantIdentifier`. Submit -> `apiErrorAlert(result.error)` on failure (which renders
  the field-attributed `tenantIdentifierAlreadyExists` message, AC-003, AC-068, AC-069) or
  `successToast` + `router.push('/admin/tenants/list')` on success. `TenantUpdateForm` guards its
  render order: `isLoading -> <Loader />`, `error -> <ApiErrorMessages />`, no data ->
  `t('page.tenants.notFound')`, then the form.
- `TenantMemberTable` - `useTenantMemberListQuery({ tenantId, ...url })`; columns
  `table.columns.userName`, `table.columns.email`, `table.columns.roles`, `actions`. Add gated by
  `Allow.TenantMember_Add`, roles by `Allow.TenantMember_UpdateRoles`, remove by
  `Allow.TenantMember_Remove` with `confirmDeleteAlert`. A rejected removal or role change surfaces
  `error.server.lastTenantAdministrator` through `apiErrorAlert` (AC-019).
- `TenantMemberAddModal` - `Modal` + Formik; a `FormLazySelect` over `useLazyUserListQuery` for the
  account and a `FormLazyMultiSelect` for the tenant's roles (OQ-2 is answered: they come from the role list with its optional tenant filter; see how the target
  tenant's roles are fetched); `useTenantMemberAddMutation`. A duplicate surfaces
  `error.server.duplicateTenantMembership` (AC-015).
- `TenantMemberRolesModal` - the same role picker, pre-filled from the row,
  `useTenantMemberUpdateRolesMutation`; a `concurrentModification` failure surfaces through
  `apiErrorAlert` (AC-081).
- `SelectTenantView` - lists `authState.tenants` as buttons, runs the §6.4 switch flow, and renders
  a reason banner driven by `?reason=` (`tenantSuspended`, `tenantMembershipRevoked`,
  `tenantNotFound`, `noActiveTenant`) so the user is told why they are here (AC-029, AC-070).
- `NoTenantView` - explains that the account belongs to no active tenant, hosts `TenantOnboardForm`,
  links to `/profile` and `/change-password`, and offers sign-out (AC-126, AC-143).
  `TenantOnboardForm` reuses the same Yup schema shape as `TenantCreateForm` (AC-134); on success it
  runs the §4 reset sequence and pushes `/admin`.

---

## 6. Shared components, state and flows

### 6.1 Created shared component

| File | Component | Notes | Serves |
| --- | --- | --- | --- |
| `components/custom/tenant-switcher.tsx` | `TenantSwitcher` | `components/custom/` is the folder for app-specific composites tied to a domain concept (`ui-component`). Reuses `Dropdown` from `@/components/ui` with the existing RTL placement idiom (`useAppSelector(state => state.theme.rtlClass) === 'rtl'`), shows `authState.activeTenant?.name` (or `page.tenants.switcher.noTenant`), and lists `authState.tenants`. Renders **nothing** when `tenants.length === 0` and renders a non-interactive label when `tenants.length === 1` (AC-073). Exported from `components/custom/index.ts`, rendered in `components/layouts/header.tsx` beside `NotificationBell` / `ThemeChanger` / `LanguageDropdown` / `NavUser` | AC-025, AC-029, AC-073, AC-077 |

**No new component is added to `components/ui/`.** The screens need no primitive the library lacks:
the data-table system, `Badge`, `Modal`, `Dropdown`, `Button`, `Loader`, `ApiErrorMessages`,
`LocalizedLink` and the whole `components/ui/form` set already exist.

### 6.2 Shared components reused unchanged

`AdminPageContent`; `DataTableProvider`, `DataTable`, `DataTableToolbar`, `DataTablePagination`;
`Badge`, `Button`, `IconButton`, `Modal`, `Dropdown`, `Loader`, `Tooltip`, `Truncated`, `DateView`,
`LocalizedLink`, `ApiErrorMessages`; `FormInput`, `FormSelect`, `FormLazySelect`,
`FormLazyMultiSelect`; `confirmDeleteAlert`, `successToast`, `errorAlert`, `apiErrorAlert`,
`exportData`, `isAllowed`; `useTableUrlState`, `useLocalizedRouter`, `useTranslation`,
`getServerTranslation`.

### 6.3 Redux state (`redux-state`; no new slice, per D19)

`store/slices/authSlice.ts`:

| Member | Shape | Serves |
| --- | --- | --- |
| `state.activeTenant` | `GetUserInfoTenant \| undefined` | AC-073, AC-123, AC-140 |
| `state.tenants` | `GetUserInfoTenant[]` | AC-024, AC-029 |
| `state.tenantError` | `string \| undefined` - one of the tenant error codes | AC-029, AC-070 |
| `setUserInfo` (existing) | additionally sets `activeTenant` and `tenants` from the payload and clears `tenantError` | AC-024, AC-108, AC-127 |
| `setTenantError` (new) | records the code the middleware saw | AC-070 |
| `clearTenantError` (new) | cleared once the reason banner has been shown | AC-070 |
| `signout` (existing) | additionally clears `activeTenant`, `tenants`, `tenantError` | AC-125 |

`lib/utils/authentication-and-authorization.ts`: `AuthState` widens with the three fields above.
`isAllowed` keeps its current logic and signature - permissions arrive already scoped to the active
tenant (AC-027), and platform permissions must keep evaluating while no tenant is active (AC-046),
so gating `isAllowed` on `activeTenant` would break platform administration.
`store/slices/index.ts` exports the two new actions.

`store/middlewares/rtk-error-middleware.ts` is **extended** rather than duplicated (it is already
the one place rejected RTK Query actions are inspected): when a rejected payload carries one of
`tenantSuspended`, `tenantMembershipRevoked`, `tenantNotFound`, `noActiveTenant` or
`notTenantMember`, it dispatches `setTenantError(code)`. It never signs the user out - AC-070 and
plan D9 forbid that. `store/index.tsx` therefore needs no change.

### 6.4 Flows

| Flow | Steps | Serves |
| --- | --- | --- |
| **Sign-in** (`app/[lang]/(auth)/signin/_components/signin-form.tsx`) | after `getUserInfo`: `activeTenant` present -> existing redirect logic (`redirect` param, else `/admin`, else `/`); no `activeTenant` and `tenants.length > 0` -> `/select-tenant`; `tenants.length === 0` -> `/no-tenant` | AC-123, AC-126, AC-140, AC-142 |
| **Entering the app** (`App.tsx`) | after `setUserInfo`, when authenticated and the path is tenant-scoped (`/admin/...`) and `activeTenant` is absent: replace with `/select-tenant` when `tenants.length > 0`, `/no-tenant` otherwise. `/profile`, `/change-password`, `/select-tenant`, `/no-tenant`, `/unauthorized` and public routes are exempt, so account self-service stays reachable | AC-050, AC-051, AC-122, AC-126, AC-140 |
| **Tenant failure mid-session** (`App.tsx`, reading `authState.tenantError`) | replace with `/select-tenant?reason=<code>` when `tenants.length > 0`, else `/no-tenant`, then `clearTenantError()`. The user stays authenticated and is never signed out | AC-020, AC-029, AC-070, AC-127 |
| **Switching** (`TenantSwitcher`, `SelectTenantView`) | `tenantSwitch({ tenantId })` -> on error `apiErrorAlert` and stop -> `resetApiState()` -> `getUserInfo()` -> `setUserInfo(fresh)` -> `setUnreadCount(0)` -> `successToast` with `page.tenants.switcher.switchSuccess` -> `router.push('/admin')` | AC-025, AC-028, AC-056, AC-108, AC-132, AC-139 |
| **Onboarding** (`TenantOnboardForm`) | `tenantOnboard({ name, identifier })` -> the same reset sequence -> `router.push('/admin')` | AC-133, AC-134, AC-143 |
| **Sign-out** (`components/custom/nav-user.tsx`) | existing `signoutApi` -> `dispatch(signout())` -> `dispatch(appApi.util.resetApiState())` -> `router.push('/signin')` | AC-125 |

---

## 7. Permission mirror entries (`src/frontend/web/allow.ts`)

The map stays identical to `Permissions/Allow.cs` (plan §4, `permissions` skill). Added:

| Key | Value | Gates | Serves |
| --- | --- | --- | --- |
| `Tenant_View` | `'Tenant.View'` | `/admin/tenants/list`, tenant detail | AC-044, AC-066, AC-071 |
| `Tenant_Create` | `'Tenant.Create'` | `/admin/tenants/create`, the create link | AC-002, AC-044, AC-071 |
| `Tenant_Update` | `'Tenant.Update'` | `/admin/tenants/update/{id}`, the row action | AC-005, AC-071 |
| `Tenant_Suspend` | `'Tenant.Suspend'` | the suspend row action | AC-006, AC-071 |
| `Tenant_Reactivate` | `'Tenant.Reactivate'` | the reactivate row action | AC-008, AC-071 |
| `Tenant_Delete` | `'Tenant.Delete'` | the delete row action | AC-009, AC-071 |
| `TenantMember_View` | `'TenantMember.View'` | `/admin/tenants/members/{id}`, the members row action | AC-021, AC-072 |
| `TenantMember_Add` | `'TenantMember.Add'` | the add-member control | AC-014, AC-072 |
| `TenantMember_UpdateRoles` | `'TenantMember.UpdateRoles'` | the change-roles control | AC-017, AC-072 |
| `TenantMember_Remove` | `'TenantMember.Remove'` | the remove-member control | AC-018, AC-072 |
| `Platform_Administration` | `'Platform.Administration'` | mirrored for completeness and for in-page checks; it gates no web route in this feature (the Hangfire dashboard is served by the API) | AC-046, AC-047, AC-141 |

No existing entry is renamed or removed.

## 8. Route-guard entries (`src/frontend/web/auth-urls.ts`)

`proxy.ts` reads these for the session check; `App.tsx`,
`components/layouts/sidebar/index.tsx` and `components/layouts/search-component.tsx` read them for
the permission check - which is what makes AC-074 automatic for both the sidebar and global search,
and what redirects a direct navigation to `/unauthorized`.

| Entry | Serves |
| --- | --- |
| `{ url: '/admin/tenants/list', permissions: [Allow.Tenant_View] }` | AC-071, AC-074 |
| `{ url: '/admin/tenants/create', permissions: [Allow.Tenant_Create] }` | AC-071, AC-074 |
| `{ url: '/admin/tenants/update/{id}', permissions: [Allow.Tenant_Update] }` | AC-071, AC-074 |
| `{ url: '/admin/tenants/members/{id}', permissions: [Allow.TenantMember_View] }` | AC-072, AC-074 |
| `{ url: '/select-tenant' }` | authenticated, no permission - AC-142 |
| `{ url: '/no-tenant' }` | authenticated, no permission - AC-126 |

No two entries match the same pathname (`getMatchedAuthUrl` throws otherwise): `update/{id}` and
`members/{id}` differ in their literal segment.

## 9. Navigation entries (`src/frontend/web/nav-items.ts`)

Added to the existing `navigation.administration` group, after `navigation.roles`: a
`navigation.tenants` item with `url: '/admin/tenants/list'` and `icon: Building2`
(`lucide-react`), whose children are `navigation.tenantsList` (`/admin/tenants/list`),
`navigation.tenantsCreate` (`/admin/tenants/create`), `navigation.tenantsUpdate`
(`/admin/tenants/update/{id}`, `show: false`) and `navigation.tenantsMembers`
(`/admin/tenants/members/{id}`, `show: false`).

Plus two hidden top-level entries beside the existing `changePassword` / `profile` pair, so those
routes resolve a title without appearing in the menu: `navigation.selectTenant`
(`/select-tenant`, `show: false`) and `navigation.noTenant` (`/no-tenant`, `show: false`).

Nav items carry no permission field - the sidebar derives visibility from `auth-urls.ts` (§8), which
is how AC-074 is met without duplicating the rule.

## 10. Global-search entries (`src/frontend/web/searchable-items.ts`)

| Entry | Serves |
| --- | --- |
| `{ title: 'search.tenants', url: '/admin/tenants/list' }` | AC-074, AC-141 |
| `{ title: 'search.tenantsCreate', url: '/admin/tenants/create' }` | AC-074, AC-141 |

`search-component.tsx` filters each entry through `getMatchedAuthUrl` + `isAllowed`, so both
disappear for a caller lacking the permission (AC-074). `/select-tenant` and `/no-tenant` are
deliberately **not** searchable - they are routed to, not navigated to.

---

## 11. Translation keys

Every key below is added to `public/locales/en.json` **and to all eight shipped locale files**
(`ar, en, es, fr, hi, ru, ur, zh`) in one owning task, English authoritative (AC-075, AC-076; plan
§7). No new namespace is introduced. Interpolation is `${name}` in the JSON value.

### `page.tenants.*`

| Key | English value | Serves |
| --- | --- | --- |
| `page.tenants.title` | Tenants | AC-071 |
| `page.tenants.list.title` | Tenants List | AC-071 |
| `page.tenants.create.title` | Create Tenant | AC-071 |
| `page.tenants.update.title` | Update Tenant | AC-071 |
| `page.tenants.notFound` | Tenant not found | AC-010, AC-071 |
| `page.tenants.systemCreated` | System-created | AC-011 |
| `page.tenants.createSuccess` | Tenant created successfully | AC-002 |
| `page.tenants.updateSuccess` | Tenant updated successfully | AC-005 |
| `page.tenants.deleteTitle` | Delete Tenant | AC-009 |
| `page.tenants.deleteConfirm` | Are you sure you want to delete this tenant? | AC-009 |
| `page.tenants.deleteSuccess` | Tenant deleted successfully | AC-009 |
| `page.tenants.suspendTitle` | Suspend Tenant | AC-006 |
| `page.tenants.suspendConfirm` | Are you sure you want to suspend this tenant? Its members lose access until it is reactivated. | AC-006, AC-007 |
| `page.tenants.suspendSuccess` | Tenant suspended successfully | AC-006 |
| `page.tenants.reactivateTitle` | Reactivate Tenant | AC-008 |
| `page.tenants.reactivateConfirm` | Are you sure you want to reactivate this tenant? | AC-008 |
| `page.tenants.reactivateSuccess` | Tenant reactivated successfully | AC-008 |
| `page.tenants.status.active` | Active | AC-001, AC-065 |
| `page.tenants.status.suspended` | Suspended | AC-006, AC-065 |
| `page.tenants.members.title` | Tenant Members | AC-072 |
| `page.tenants.members.addTitle` | Add Member | AC-014 |
| `page.tenants.members.addButton` | Add Member | AC-014 |
| `page.tenants.members.addSuccess` | Member added successfully | AC-014 |
| `page.tenants.members.rolesTitle` | Change Member Roles | AC-017 |
| `page.tenants.members.rolesButton` | Change Roles | AC-017 |
| `page.tenants.members.rolesSuccess` | Member roles updated successfully | AC-017 |
| `page.tenants.members.removeTitle` | Remove Member | AC-018 |
| `page.tenants.members.removeConfirm` | Are you sure you want to remove this member from the tenant? | AC-018 |
| `page.tenants.members.removeSuccess` | Member removed successfully | AC-018 |
| `page.tenants.switcher.label` | Tenant | AC-073 |
| `page.tenants.switcher.switchTo` | Switch tenant | AC-025, AC-073 |
| `page.tenants.switcher.switchSuccess` | You are now working in ${tenant} | AC-025 |
| `page.tenants.switcher.noTenant` | No tenant selected | AC-073, AC-140 |

### `page.selectTenant.*`

| Key | English value | Serves |
| --- | --- | --- |
| `page.selectTenant.title` | Select Tenant | AC-142 |
| `page.selectTenant.description` | Choose the tenant you want to work in. | AC-140, AC-142 |
| `page.selectTenant.suspendedReason` | The tenant you were working in has been suspended. Choose another tenant to continue. | AC-029, AC-070 |
| `page.selectTenant.revokedReason` | Your membership in that tenant has been revoked. Choose another tenant to continue. | AC-020, AC-070 |
| `page.selectTenant.unavailableReason` | That tenant is no longer available. Choose another tenant to continue. | AC-010, AC-127 |
| `page.selectTenant.noSelectionReason` | Choose a tenant before opening this screen. | AC-023, AC-140 |

### `page.noTenant.*`

| Key | English value | Serves |
| --- | --- | --- |
| `page.noTenant.title` | No Tenant | AC-126 |
| `page.noTenant.description` | You do not belong to an active tenant yet. Create your own tenant, or ask an administrator to add you to theirs. | AC-050, AC-122, AC-126 |
| `page.noTenant.createTitle` | Create Your Tenant | AC-143 |
| `page.noTenant.createButton` | Create Tenant | AC-143 |
| `page.noTenant.createSuccess` | Tenant created. You are now its administrator. | AC-133, AC-135 |

### `navigation.*` and `search.*`

| Key | English value | Serves |
| --- | --- | --- |
| `navigation.tenants` | Tenants | AC-071, AC-074 |
| `navigation.tenantsList` | List | AC-071 |
| `navigation.tenantsCreate` | Create | AC-071 |
| `navigation.tenantsUpdate` | Update | AC-071 |
| `navigation.tenantsMembers` | Members | AC-072 |
| `navigation.selectTenant` | Select Tenant | AC-142 |
| `navigation.noTenant` | No Tenant | AC-126 |
| `search.tenants` | Tenants | AC-074 |
| `search.tenantsCreate` | Create Tenant | AC-074 |

### `table.*`

| Key | English value | Serves |
| --- | --- | --- |
| `table.columns.identifier` | Identifier | AC-001, AC-064 |
| `table.filter.tenantStatus` | Tenant Status | AC-065 |
| `table.filter.allStatuses` | All Statuses | AC-065 |
| `table.filter.suspended` | Suspended | AC-065 |

Reused unchanged: `table.columns.name`, `table.columns.status`, `table.columns.updated`,
`table.columns.userName`, `table.columns.email`, `table.columns.roles`, `table.actions`,
`table.filter.active`, `table.filter.button`, `table.filter.search`, `table.filter.clear`,
`table.export.*`, `table.noRecords`, `table.pagination.showingEntries`, `table.createLink`.

### `form.*` and `validation.*`

| Key | English value | Serves |
| --- | --- | --- |
| `form.label.tenantName` | Tenant Name | AC-100 |
| `form.label.tenantIdentifier` | Identifier | AC-101 |
| `form.label.user` | User | AC-014 |
| `form.placeholder.tenantName` | Enter tenant name | AC-100 |
| `form.placeholder.tenantIdentifier` | Enter identifier (for example acme) | AC-101 |
| `form.placeholder.user` | Select user | AC-014 |
| `validation.tenantIdentifier` | Use lower-case letters, digits and single hyphens, starting and ending with a letter or digit | AC-004, AC-101 |

Reused unchanged: `validation.required`, `validation.minLength`, `validation.maxLength`,
`validation.atLeastOneSelected`, `form.label.roles`, `form.placeholder.roles`, `common.submit`,
`common.cancel`, `common.deleteConfirm`.

### `error.server.*` (the AC-067 code set, one entry per code; AC-069)

| Key | English value | Serves |
| --- | --- | --- |
| `error.server.tenantNotFound` | Tenant not found | AC-010, AC-069 |
| `error.server.tenantSuspended` | This tenant is suspended | AC-007, AC-069, AC-070 |
| `error.server.notTenantMember` | You are not a member of this tenant | AC-026, AC-069 |
| `error.server.noActiveTenant` | No active tenant is selected | AC-023, AC-069 |
| `error.server.tenantIdentifierAlreadyExists` | Tenant identifier already exists | AC-003, AC-068 |
| `error.server.duplicateTenantMembership` | This user is already a member of this tenant | AC-015 |
| `error.server.lastTenantAdministrator` | The last tenant administrator cannot be removed | AC-019 |
| `error.server.systemCreatedTenantCannotBeModified` | System-created tenant cannot be modified | AC-011 |
| `error.server.crossTenantFileAccess` | This file belongs to another tenant | AC-058 |
| `error.server.tenantMembershipRevoked` | Your membership in this tenant has been revoked | AC-020, AC-070 |
| `error.server.platformPermissionNotGrantable` | Platform permissions cannot be granted to a tenant role | AC-041 |
| `error.server.concurrentModification` | This record was changed by someone else. Reload and try again. | AC-081 |

Each key's last segment must match a constant in `ErrorHandling/ErrorCodes.cs` exactly
(`api-error-handling`); `getApiErrorMessages` resolves them, so no raw code or untranslated key ever
reaches a screen (AC-069).

---

## 12. RTL and dark mode

All new markup uses logical spacing utilities (`ms-`, `me-`, `ps-`, `pe-`, `inset-s-`, `inset-e-`)
and pairs every colour with its `dark:` counterpart; `TenantSwitcher` picks its dropdown placement
from `state.theme.rtlClass` exactly as `NavUser` does (AC-077). No direction-specific class (`ml-`,
`pl-`, `left-`) appears in a new file.

## 13. Traceability - web-side criteria to elements

| AC | Element |
| --- | --- |
| AC-024 | `GetUserInfoResponse.tenants` (§2.4) |
| AC-025, AC-139 | `tenantSwitch` (§3) + the switch flow (§6.4) |
| AC-028, AC-132 | `resetApiState()` in the switch / onboard / sign-out sequences (§4) |
| AC-029, AC-070 | middleware -> `tenantError` -> `/select-tenant?reason=` (§6.3, §6.4, §11) |
| AC-044, AC-074 | `allow.ts` (§7) + `auth-urls.ts` (§8) + nav and search (§9, §10) |
| AC-046, AC-141 | `/admin/tenants/*` screens gated by permission inside the same app (§5, §7) |
| AC-050, AC-122, AC-126 | `/no-tenant` + the `App.tsx` routing rule (§5, §6.4) |
| AC-051 | `/profile` and `/change-password` exempt from the tenant routing rule (§6.4) |
| AC-056 | `setUnreadCount(0)` on switch (§4, §6.4) |
| AC-061..AC-066 | `TenantListRequest` params + `useTableUrlState` filters (§2.1, §3, §5) |
| AC-069 | `error.server.*` keys (§11) |
| AC-071 | list / create / update / suspend / reactivate / delete surfaces, each permission-gated (§5, §7) |
| AC-072 | `/admin/tenants/members/{id}` (§5) |
| AC-073 | `TenantSwitcher` in `header.tsx` (§6.1) |
| AC-075, AC-076 | every string is a key, present in all eight locale files (§11) |
| AC-077 | §12 |
| AC-092, AC-132 | the `AuthState` / `authSlice` shape the frontend tests assert against (§6.3; the test files themselves belong to the test contract) |
| AC-123 | sign-in redirect when `activeTenant` is present (§6.4) |
| AC-124, AC-125 | no browser storage; `signout` + `resetApiState` on sign-out (§4, §6.4) |
| AC-127 | `setUserInfo` overwrites `activeTenant` from the server on every load (§2.4, §6.3) |
| AC-133, AC-134, AC-143 | `tenantOnboard` + `TenantOnboardForm` on `/no-tenant` (§3, §5) |
| AC-140, AC-142 | `/select-tenant` + the sign-in and `App.tsx` rules (§5, §6.4) |

## 14. Explicitly unchanged

- `app/[lang]/admin/(identity)/users/**` and `roles/**` - AC-093..AC-096 and AC-110..AC-115 change
  which rows the API returns, not the screens. No column, filter, form or route changes.
- `app/[lang]/admin/(identity)/roles/change-permissions/[id]/**` - AC-114 and AC-115 filter the
  catalogue server-side; the screen renders whatever `getDefinePermissions` returns.
- `components/notifications/**` and `hooks/use-notification-hub.ts` - AC-052..AC-056 are row
  filters; the badge and panel are untouched apart from the `setUnreadCount(0)` dispatch that lives
  in the switch handler.
- `store/api/_app-api.ts` - the reauth flow is unchanged; a tenant failure is a 403 that must not
  trigger a refresh or a sign-out (plan D9).
- `store/index.tsx` - no new reducer and no new middleware registration (the existing
  `rtkErrorMiddleware` is extended instead).
- `proxy.ts` - it can only see the auth cookie, not membership, so tenant routing stays a client
  decision in `App.tsx`; the guard entries it reads are added in `auth-urls.ts`.
- `i18n/config.ts`, `i18n/server.ts`, `store/slices/themeConfigSlice.tsx` - no locale is added or
  removed, and the generator's regex-rewritten shapes stay intact.
- `components/ui/**`, except the `FileUpload` change contingent on OQ-1.

## 15. Open questions

All questions raised while these contracts were written have been answered by the requester. The
decisions, in the order they were put:

1. **First-member administrator role.** The tenant's system-created administrator role is assigned to
   the first member automatically, whether or not the add request names it, so a tenant can never
   exist without an administrator. The add request's role set is unioned with that role for the first
   member; for every later member it is taken exactly as supplied.
2. **Tenant rename tier.** The tenant update permission stays at platform tier. A tenant administrator
   manages members and roles but never renames the tenant or changes its identifier, so the update
   endpoint keeps its platform-only authorization and gains no member branch.
3. **Account-owned uploads.** The existing upload route carries a boolean field marking a file
   account-owned; there is no second route. The profile avatar upload sets it, and every other upload
   leaves it unset and is attributed to the active tenant.
4. **Identifier case clash.** A tenant identifier is trimmed and lower-cased before the uniqueness
   comparison, so submitting an identifier that differs from an existing one only in case is refused
   as a duplicate on the identifier field, not as a character-set validation failure.
5. **Assignable roles for a tenant administered from the platform.** The existing role list accepts an
   optional tenant to filter by, honoured only for a caller holding platform-tier authority and
   ignored for every other caller. There is no dedicated route for a tenant's roles, and the role
   response shape is unchanged.

Two smaller points were settled without a question, both recorded here so a reviewer sees the choice
rather than an omission. The twelve tenant error codes are listed literally in both the web locale
test and the backend error-code test rather than sharing a source, because a duplicated literal list
is the point of a guard test. The background-job dashboard refusal is proved by a unit test on the
authorization filter, without an additional end-to-end request.

