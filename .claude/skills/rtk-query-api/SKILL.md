---
name: rtk-query-api
description: Add or change an RTK Query API slice and its DTOs under src/frontend/web/store/api — injectEndpoints on the shared appApi, DTO files mirroring the backend, and the rules for when to use cache tags (providesTags/invalidatesTags) and when to skip them.
---

# RTK Query APIs

## Layout

```
store/api/
  _app-api.ts                       # the ONE createApi — never call createApi again
  index.ts                          # barrel: base DTO types + every feature export
  base/dto/*.ts                     # BaseDto, ListDto, ListRequestDto, RequestBase, …
  <feature>/
    index.ts                        # barrel for the feature
    <area>/
      <area>-api.ts
      <area>-dtos.ts
```

`<feature>` and `<area>` mirror the backend: `identity/users`, `identity/roles`,
`identity/account`, `notifications/notifications`, `file-management/files`.

## DTOs first

`<area>-dtos.ts` mirrors the C# request/response classes **by name**, camelCased. `Guid` → `string`,
`DateTime` → `string`. Extend the shared bases from `@/store/api`:

```ts
import { BaseDto, RequestBase, GenericAuditableDto, ListRequestDto, ListDto } from '@/store/api'

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

Every exported interface gets a one-line JSDoc.

## The API slice

Attach to the shared `appApi`; it already carries the base URL, credentials, and the
`baseQueryWithReauth` that refreshes the token through an `async-mutex` and signs the user out on
failure.

```ts
import { appApi } from '@/store/api/_app-api'
import { UserCreateRequest, UserCreateResponse, … } from './users-dtos'

/**
 * RTK Query API for user management: CRUD on users and a paginated listing
 * endpoint… Uses the 'Users' tag type for cache invalidation across the feature.
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
`userList`, `notificationMarkAsRead`. Hooks therefore come out as `useUserCreateMutation`,
`useUserListQuery`, `useLazyUserListQuery`.

Then export the slice, the hooks and the DTO types from `store/api/<feature>/index.ts`, and make
sure `store/api/index.ts` re-exports the feature. Components import from the feature barrel:
`import { useUserListQuery, UserListDto } from '@/store/api/identity'`.

## When to use tags — and when not to

Use a tag type when the data is a **server-owned collection that this app also mutates**, so a
mutation must refresh lists and details. That is `Users`, `Roles`, `Notifications`. The tag type is
the PascalCase plural of the area, and it is declared with `enhanceEndpoints({ addTagTypes: [...] })`
on that feature's slice only.

Do **not** add tags when there is nothing to invalidate:

- `account-api.ts` — signin/signup/password/profile calls; session state lives in `authSlice`
  and the reauth flow, not in the cache.
- `permissions-api.ts` — the permission catalog is static for the life of a deployment.
- `files-api.ts` — uploads/downloads/deletes are addressed by file name, and nothing lists them.
- Individual polled endpoints such as `notificationGetUnreadCount`, which refetches on its own
  interval; tagging it would only add redundant refetches.

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

// an aggregate that any mutation in the area affects
providesTags: ['Notifications'],
```

## Query shapes

```ts
// mutation with a body
userCreate: builder.mutation<UserCreateResponse, UserCreateRequest>({
  query: (input) => ({ url: '/users', method: 'POST', body: input }),
  invalidatesTags: ['Users'],
}),

// update: id goes in the path, and is stripped from the body
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
  `<ApiErrorMessages error={error} />` on failure.
- Mutation: `const [createUser, { isLoading }] = useUserCreateMutation()`, then
  `const result = await createUser(payload)`; on `result.error` call `apiErrorAlert(result.error)`
  and return; on success `successToast.fire({ text: t('…') })` and navigate.
- Lazy query for on-demand fetches (export "all rows", async selects):
  `const [fetchUsers] = useLazyUserListQuery()`.
