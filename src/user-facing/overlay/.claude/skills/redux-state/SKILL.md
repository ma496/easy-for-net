---
name: redux-state
description: Add or change client state in src/frontend/web/store — Redux Toolkit slices, store registration, typed useAppSelector/useAppDispatch hooks, the auth session state (and why the public site never waits on it), and middleware. Use when you need state that is not server data, and to decide between a slice and the RTK Query cache.
---

# Client state

## Slice or RTK Query?

Server data belongs in the RTK Query cache — never copy a query result into a slice "so components
can read it". Use a slice only for state the client owns:

| Slice (store key) | Holds |
| --- | --- |
| `authSlice` (`auth`) | the session: `user` (`GetUserInfoResponse`), `isAuthenticated` — typed as `AuthState` from `@/lib/utils` — plus `isSettled`, whether this document's first read of the account info has answered (`AuthSliceState`) |
| `themeConfigSlice` (`theme`) | the color scheme (`theme`: light, dark or system, and `isDarkMode`, what it resolves to), `rtlClass`, and `sidebar` (collapsed to the icon rail from lg up, the drawer open below it). Enabled languages come from the translation dictionary, not a slice |
| `notificationsSlice` (`notifications`) | `unreadCount`, the badge (`setUnreadCount`, and `notificationReceived` counting one push) |
| `serviceAvailabilitySlice` (`serviceAvailability`) | `isUnavailable` — whether the API is unreachable |

`notificationsSlice` is the sanctioned exception to the rule above: `useNotificationHub` subscribes
to the unread-count query (polling it only while the hub is down), mirrors the number into the slice
and counts each pushed notification on top, so the badge can be read from anywhere without every
consumer subscribing to the query. `authSlice` is the other: the account info is
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
const isSettled = useAppSelector((state) => state.auth.isSettled)
const isRTL = useAppSelector((state) => state.theme.rtlClass) === 'rtl'

const dispatch = useAppDispatch()
dispatch(setUnreadCount(count))
```

Select the narrowest value a component needs so unrelated updates do not re-render it.

## Session state

- `App.tsx` reads `/account/get-info` on mount, dispatches `setUserInfo` (which derives
  `isAuthenticated` from the payload — never set it on its own), and then `markUserInfoSettled()`,
  with or without a user. `baseQueryWithReauth` re-reads the info after every successful token
  refresh (the renewed session carries the roles and permissions as they stand now), and dispatches
  `signout()` when the refresh fails — redirecting to `/signin?redirect=…` only when the current page
  needs a session (`signinRedirectAfterFailedRefresh`), so a visitor on a public page stays where they are.
- **The public site never waits on the session.** `App.tsx` holds nothing back; only `AuthGuard`
  (rendered by the admin and account layouts) waits for `isSettled` before deciding, and a public
  component that shows account state (the `SiteHeader` account menu) waits on `isSettled` for that
  small area alone, so a signed-in visitor never sees "Sign in" flash. Never gate a `(site)` page's
  content on `auth` — it is server-rendered for crawlers that have no session.
- **Ending the session** (sign-out, password change) calls `leaveSignedOut(signinHref)` — a full page
  load, so the next user of the browser starts with a fresh store.
- Permission checks read this slice through `isAllowed(authState, [Allow.X])` (`Allow.Admin_Access`
  for whether to offer the admin area at all) — see the `permissions` skill.

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
**before** `appApi.middleware`. Reducers, the middleware and the sign-out page load all have
colocated Vitest tests (`authSlice.test.ts`, `rtk-error-middleware.test.ts`, `leave-signed-out.test.ts`,
`signed-out-navigation.test.ts`) —
extend them; see `frontend-tests`.

## Checklist

- [ ] The data is genuinely client-owned (otherwise use RTK Query)
- [ ] Slice file + export from `store/slices/index.ts`
- [ ] Registered in `store/index.tsx` by `slice.name`
- [ ] Reducers side-effect free; persistence handled outside
- [ ] Consumers use `useAppSelector` / `useAppDispatch` with narrow selectors
- [ ] Reducer/middleware behaviour pinned in a colocated `*.test.ts`
