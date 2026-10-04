---
name: rtk-query-api
description: Add or change an RTK Query API slice and its DTOs under src/frontend/web/store/api — injectEndpoints on the shared appApi, DTO files mirroring the backend (including the tenant and system-created markers), feature barrels, and the rules for cache tags (providesTags/invalidatesTags, tags shared across slices, and the full cache reset when the tenant changes).
---

# RTK Query APIs

## Layout

```
store/api/
  _app-api.ts                       # the ONE createApi — never call createApi again
  index.ts                          # barrel for the shared bases only: SortDirection + base DTO types
  base/dto/*.ts                     # BaseDto, ListDto, ListRequestDto, RequestBase, SystemCreatedDto, …
  <feature>/
    index.ts                        # barrel for the feature: slices, hooks, DTO types, enums
    enums.ts                        # optional: enums the feature's DTOs share
    <area>/
      <area>-api.ts
      <area>-dtos.ts
      <area>-mappers.ts             # optional: pure DTO <-> form conversions, with a colocated test
```

`<feature>` and `<area>` mirror the backend: `identity/users`, `identity/roles`, `identity/account`,
`identity/permissions`, `notifications/notifications`, `file-management/files`, `tenancy/tenants`,
`tenancy/editions`, `tenancy/features`, `localization/localization`, `settings/settings`.

## DTOs first

`<area>-dtos.ts` mirrors the C# request/response classes **by name**, camelCased. `Guid` → `string`,
`DateTime` → `string`, a nullable → `T | null` or an optional property. Extend the shared bases from
`@/store/api`:

| Base | For |
| --- | --- |
| `BaseDto<TId>` | anything carrying `id` (get/delete requests, responses) |
| `RequestBase` | every request |
| `ListRequestDto<TId>` / `ListDto<T>` | list requests (`page`, `pageSize`, `sortField`, `sortDirection`, `search`, `all`, `includeIds`) and paged responses (`items`, `total`) |
| `GenericAuditableDto<TId>` (and the creatable/updatable variants) | rows with audit fields |
| `SystemCreatedDto` | rows the system seeded, which update/delete will refuse — the UI hides those actions |
| `HaveTenantDto` / `MayHaveTenantDto` | rows that carry `tenantId` (`string`, or `string \| null` where null is platform scope) |
| `EmptyRequest` | an endpoint that takes nothing |

Pick the marker from the entity the DTO projects, not from the fields it happens to carry.

```ts
import { BaseDto, RequestBase, GenericAuditableDto, ListRequestDto, ListDto, SystemCreatedDto } from '@/store/api'

/** Request body for creating a new user… */
export interface UserCreateRequest extends RequestBase {
  username: string
  email: string
  isActive: boolean
  roles: string[]
}

/** Request parameters for fetching a single user by id. */
export interface UserGetRequest extends BaseDto<string>, RequestBase {}

/** Paged response of users returned by the list-users endpoint. */
export interface UserListResponse extends ListDto<UserListDto> {}

/** Request parameters for the list-users endpoint… */
export interface UserListRequest extends ListRequestDto<string>, RequestBase {
  isActive?: boolean
  roleId?: string
}
```

String enums the backend serializes by name are either a TS `enum` (`TenantStatus`,
`NotificationType`, in the feature's `enums.ts`) or an `as const` object plus a derived union type
(`FeatureValueProvider` / `FeatureValueProviderName`). Every exported interface gets a one-line JSDoc.

## The API slice

Attach to the shared `appApi`; it already carries the base URL, credentials, and the
`baseQueryWithReauth` that refreshes the token through an `async-mutex`, re-reads the account info
after a successful refresh (the renewed session may have dropped its tenant), and signs the user out
when the refresh fails.

```ts
import { appApi } from '@/store/api/_app-api'
import { UserCreateRequest, UserCreateResponse, … } from './users-dtos'

/**
 * RTK Query API for user management… Uses the 'Users' tag type for cache invalidation.
 */
export const usersApi = appApi
  .enhanceEndpoints({ addTagTypes: ['Users'] })
  .injectEndpoints({
    overrideExisting: false,
    endpoints: (builder) => ({ … }),
  })

export const { useUserCreateMutation, useUserGetQuery, useLazyUserGetQuery, useUserListQuery, useLazyUserListQuery } = usersApi
```

Endpoint naming matches the backend endpoint: `userCreate`, `userUpdate`, `userDelete`, `userGet`,
`userList`, `tenantMemberAdd`, `notificationMarkAsRead`. Hooks therefore come out as
`useUserCreateMutation`, `useUserListQuery`, `useLazyUserListQuery`.

Then export the slice, the hooks, the DTO types (`export type { … }`) and any enums from
`store/api/<feature>/index.ts`. Components import from the feature barrel —
`import { useUserListQuery, UserListDto } from '@/store/api/identity'` — and shared bases from
`@/store/api`, which does **not** re-export the features. Inside `store/`, import across slices by
file path (`@/store/api/identity/account/account-api`) to avoid barrel cycles.

## When to use tags — and when not to

Use a tag type when the data is a **server-owned collection that this app also mutates**, so a
mutation must refresh lists and details: `Users`, `Roles`, `Notifications`, `Tenants`,
`TenantMembers`, `Editions`, `FeatureValues`, `MyFeatures`, `Settings`, `LocalizationText`,
`LocalizationLanguage`. Name it after the area in PascalCase and declare it with
`enhanceEndpoints({ addTagTypes: [...] })` on every slice that provides **or invalidates** it — a slice
may re-declare another area's tag (`tenantsApi` declares `Users` so a member change refreshes the user
list and the seat count; `editionsApi` declares `Tenants` because deleting a plan changes the tenants
list).

Do **not** add tags when there is nothing to invalidate:

- `account-api.ts` — signin/signup/password/profile calls; session state lives in `authSlice`
  and the reauth flow, not in the cache.
- `permissions-api.ts` — the permission catalogue is static for the life of a deployment.
- `files-api.ts` — uploads/downloads/deletes are addressed by file name, and nothing lists them.
- `tenantSwitch` / `tenantExit` — changing the acting tenant changes what **every** cached query would
  answer, so the caller discards the whole store with a full page load instead (`useTenantSwitch` /
  `leaveForTenantChange` in `store/tenant-cache.ts`). Invalidating or resetting in place would refetch
  the mounted page's queries mid-switch. See the `multi-tenancy` skill.

Those slices use plain `appApi.injectEndpoints({ overrideExisting: false, endpoints })`.

## Tag patterns to copy

```ts
// list: the collection tag plus a per-row tag
providesTags: (result) => ['Users', ...(result?.items?.map((item) => ({ type: 'Users' as const, id: item.id })) ?? [])],

// single get: only the row tag
providesTags: (result, error, arg) => [{ type: 'Users', id: arg.id }],

// create: only the collection changed
invalidatesTags: ['Users'],

// update / delete / mark-as-read: collection + that row
invalidatesTags: (result, error, arg) => ['Users', { type: 'Users', id: arg.id }],

// an aggregate that any mutation in the area affects (a count, the seat usage)
providesTags: ['Users'],

// a row keyed by more than one field
providesTags: (result, error, arg) => [{ type: 'FeatureValues', id: `${arg.providerName}:${arg.providerKey}` }],

// a named sub-tag, so one invalidation can skip a query the bare type would refetch
export const NOTIFICATIONS_LIST_TAG = { type: 'Notifications' as const, id: 'LIST' }
providesTags: [{ type: 'Notifications', id: 'UNREAD_COUNT' }],
```

The notification lists provide `NOTIFICATIONS_LIST_TAG`, which the hub's push invalidates
(`use-notification-hub.ts`) without refetching the unread count it has already counted; the
mutations invalidate the bare `Notifications` type and so refresh the badge too.

A mutation that changes what the **signed-in account** is (its tenants, its roles) also has to refresh
`authSlice`, which is not in the cache: `onQueryStarted` awaits `queryFulfilled`, re-reads
`accountApi.endpoints.getUserInfo` with `forceRefetch: true`, and dispatches `setUserInfo` —
`refreshOwnTenants` in `tenants-api.ts` is the model.

## Query shapes

```ts
// mutation with a body
userCreate: builder.mutation<UserCreateResponse, UserCreateRequest>({
  query: (input) => ({ url: '/users', method: 'POST', body: input }),
  invalidatesTags: ['Users'],
}),

// update: ids go in the path, and are stripped from the body
userUpdate: builder.mutation<UserUpdateResponse, UserUpdateRequest>({
  query: (input) => ({ url: `/users/${input.id}`, method: 'PUT', body: { ...input, id: undefined } }),
  invalidatesTags: (result, error, arg) => ['Users', { type: 'Users', id: arg.id }],
}),

// list: destructure the request into params so undefined values drop out of the query string
userList: builder.query<UserListResponse, UserListRequest>({
  query: ({ page, pageSize, sortField, sortDirection, search, all, includeIds, isActive, roleId }) => ({
    url: '/users',
    params: { page, pageSize, sortField, sortDirection, search, all, includeIds, isActive, roleId },
    method: 'GET',
  }),
}),

// optional filters that must be omitted entirely when unset
params: { …, ...(isRead !== null && isRead !== undefined ? { isRead } : {}), ...(group ? { group } : {}) },

// file upload: build FormData in the query
query: (input) => { const body = new FormData(); body.append('file', input.file); return { url: '/file-management/upload', method: 'POST', body } },

// binary download
query: (input) => ({ url: `/file-management/${input.fileName}`, method: 'GET', responseHandler: (response) => response.blob() }),
```

## Consuming in components

- Query: `const { data, isFetching, error } = useUserListQuery({ … })` — render
  `<ApiErrorMessages error={error} />` on failure. Pass `{ skip: true }` when the call cannot apply
  yet (`useMyFeaturesQuery(undefined, { skip: !hasActiveTenant })`).
- Mutation: `const [createUser, { isLoading }] = useUserCreateMutation()`, then
  `const result = await createUser(payload)`; on `result.error` call `apiErrorAlert(result.error)`
  and return; on success `successToast.fire({ text: t('…') })` and navigate. See
  `api-error-handling` for how the error codes are translated.
- Lazy query for on-demand fetches (export "all rows", async selects):
  `const [fetchUsers] = useLazyUserListQuery()`.
