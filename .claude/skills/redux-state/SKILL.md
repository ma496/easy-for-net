---
name: redux-state
description: Add or change client state in src/frontend/web/store — Redux Toolkit slices, store registration, typed useAppSelector/useAppDispatch hooks, the auth/tenant session state and the tenant-change reset sequence, and middleware. Use when you need state that is not server data, and to decide between a slice and the RTK Query cache.
---

# Client state

## Slice or RTK Query?

Server data belongs in the RTK Query cache — never copy a query result into a slice "so components
can read it". Use a slice only for state the client owns:

| Slice (store key) | Holds |
| --- | --- |
| `authSlice` (`auth`) | the session: `user` (`GetUserInfoResponse`), `isAuthenticated`, `activeTenant`, `tenants` — typed as `AuthState` from `@/lib/utils` |
| `themeConfigSlice` (`theme`) | theme/dark mode, layout, menu, `rtlClass`, animation, navbar, semidark, sidebar, locale (enabled languages come from the translation dictionary, not a slice) |
| `notificationsSlice` (`notifications`) | the unread badge count |
| `serviceAvailabilitySlice` (`serviceAvailability`) | `isUnavailable` — whether the API is unreachable |

`notificationsSlice` is the sanctioned exception to the rule above: `useNotificationHub` polls the
unread-count query and mirrors the number into the slice so the badge can be read from anywhere
without every consumer subscribing to the query. `authSlice` is the other: the account info is
server data, but it *is* the session every guard reads, so it is held in a slice and replaced
wholesale by `setUserInfo`.

## Adding a slice

`store/slices/<name>.ts` — existing files mix `camelCaseSlice.ts` (`authSlice.ts`,
`notificationsSlice.ts`, `themeConfigSlice.tsx`) and kebab-case (`service-availability-slice.ts`);
prefer kebab-case for a new file:

```ts
import { createSlice } from '@reduxjs/toolkit'

interface ServiceAvailabilityState {
  isUnavailable: boolean
}

const initialState: ServiceAvailabilityState = { isUnavailable: false }

/**
 * Tracks whether the backend API is unreachable so the app shell can show
 * an in-place service-unavailable screen without changing the current URL.
 */
export const serviceAvailabilitySlice = createSlice({
  name: 'serviceAvailability',
  initialState,
  reducers: {
    showServiceUnavailable(state) {
      state.isUnavailable = true
    },
    clearServiceUnavailable(state) {
      state.isUnavailable = false
    },
  },
})

export const { showServiceUnavailable, clearServiceUnavailable } = serviceAvailabilitySlice.actions
```

Then:

1. Re-export the slice and its actions from `store/slices/index.ts`.
2. Register it in `store/index.tsx` keyed by `slice.name`:

```ts
export const store = configureStore({
  reducer: {
    [themeConfigSlice.name]: themeConfigSlice.reducer,
    [appApi.reducerPath]: appApi.reducer,
    [authSlice.name]: authSlice.reducer,
    [notificationsSlice.name]: notificationsSlice.reducer,
    [serviceAvailabilitySlice.name]: serviceAvailabilitySlice.reducer,
  },
  middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(rtkErrorMiddleware, appApi.middleware),
  devTools: process.env.NODE_ENV !== 'production',
})
```

`RootState` and `AppDispatch` are inferred from the store, so a new slice is immediately typed.

Reducers must stay **deterministic and side-effect free**. Persistence and DOM work (writing
`localStorage`, toggling the `dark` class, setting `dir` on `<html>`) happen in `App.tsx` / provider
components reacting to state — `themeConfigSlice` is the model to follow. `setLocalStorageValue` /
`getLocalStorageValue` from `@/lib/utils` wrap JSON persistence for new values.

## Reading and dispatching

Always the typed hooks from `@/store/hooks`, never bare `useSelector`/`useDispatch`:

```ts
const authState = useAppSelector((state) => state.auth)
const hasActiveTenant = useAppSelector((state) => state.auth.activeTenant !== undefined)
const isRTL = useAppSelector((state) => state.theme.rtlClass) === 'rtl'

const dispatch = useAppDispatch()
dispatch(setUnreadCount(count))
```

Select the narrowest value a component needs so unrelated updates do not re-render it.

## Session and tenant state

- `App.tsx` reads `/account/get-info` on mount and dispatches `setUserInfo`, which derives
  `isAuthenticated`, `activeTenant` and `tenants` from the payload — never set those fields on their
  own. `baseQueryWithReauth` re-reads the info after every successful token refresh (a renewal can
  drop a suspended or left tenant), and dispatches `signout()` then redirects to
  `/signin?redirect=…` when the refresh fails.
- **Changing the acting tenant** goes through `useTenantSwitch()` (`enterTenant(id)` /
  `exitTenant()`), which calls `leaveForTenantChange(href, notice)` from `store/tenant-cache.ts`: a
  full page load to `/admin`, so the new document starts with a fresh store and nothing cached for the
  previous tenant can render; `TenantChangeNotice` shows the success toast on arrival. Never reset the
  cache in place — the still-mounted page refetches under the new session and flashes a 403. Do not
  write a second switch path; see `multi-tenancy`.
- **Ending the session** (sign-out, password change) calls `leaveSignedOut(signinHref)` — a full page
  load, so the next user of the browser starts with a fresh store.
- Permission checks read this slice through `isAllowed(authState, [Allow.X])` and scope checks through
  `isPathAvailable(authState.user, path)` — see the `permissions` skill.

## Middleware

`store/middlewares/rtk-error-middleware.ts` inspects every rejected RTK Query action and dispatches
`showServiceUnavailable()` on a `FETCH_ERROR` (the API is unreachable), which renders
`ServiceUnavailableView`. Every other failure stays the calling screen's business — it never signs the
user out or redirects. Add cross-cutting reactions to API outcomes there rather than repeating them in
components:

```ts
export const rtkErrorMiddleware: Middleware = (api) => (next) => (action: unknown) => {
  if (isRejectedWithValue(action)) { /* inspect payload/status, dispatch */ }
  return next(action)
}
```

Export new middleware from `store/middlewares/index.ts` and add it to the `concat(...)` chain
**before** `appApi.middleware`. Reducers, the middleware and the tenant-change sequence all have
colocated Vitest tests (`authSlice.test.ts`, `rtk-error-middleware.test.ts`, `tenant-cache.test.ts`) —
extend them; see `frontend-tests`.

## Checklist

- [ ] The data is genuinely client-owned (otherwise use RTK Query)
- [ ] Slice file + export from `store/slices/index.ts`
- [ ] Registered in `store/index.tsx` by `slice.name`
- [ ] Reducers side-effect free; persistence handled outside
- [ ] Consumers use `useAppSelector` / `useAppDispatch` with narrow selectors
- [ ] State that belongs to a tenant is reset by the tenant-change sequence
- [ ] Reducer/middleware behaviour pinned in a colocated `*.test.ts`
