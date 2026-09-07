---
name: redux-state
description: Add or change client state in src/frontend/web/store — Redux Toolkit slices, store registration, typed useAppSelector/useAppDispatch hooks, and middleware. Use when you need state that is not server data, and to decide between a slice and the RTK Query cache.
---

# Client state

## Slice or RTK Query?

Server data belongs in the RTK Query cache — never copy a query result into a slice "so components
can read it". Use a slice only for state the client owns:

| Slice | Holds |
| --- | --- |
| `authSlice` (`auth`) | the signed-in user (`GetUserInfoResponse`) and `isAuthenticated` |
| `themeConfigSlice` (`theme`) | dark mode, layout, menu, `rtlClass`, animation, navbar, locale, `languageList` |
| `notificationsSlice` (`notifications`) | the unread badge count |
| `serviceAvailabilitySlice` | whether the API is unreachable |

`notificationsSlice` is the sanctioned exception to the rule above: `useNotificationHub` polls the
unread-count query and mirrors the number into the slice so the badge can be read from anywhere
without every consumer subscribing to the query.

## Adding a slice

`store/slices/<name>Slice.ts` (existing files use both `camelCaseSlice.ts` and
`kebab-case-slice.ts` — match the neighbours you are adding to):

```ts
import { createSlice, PayloadAction } from '@reduxjs/toolkit'

interface NotificationsState {
  unreadCount: number
}

const initialState: NotificationsState = { unreadCount: 0 }

/**
 * Notifications slice holding the current unread notification count…
 */
export const notificationsSlice = createSlice({
  name: 'notifications',
  initialState,
  reducers: {
    setUnreadCount(state, action: PayloadAction<number>) {
      state.unreadCount = action.payload
    },
  },
})

export const { setUnreadCount } = notificationsSlice.actions
```

Then:

1. Re-export the slice and its actions from `store/slices/index.ts`.
2. Register it in `store/index.tsx` keyed by `slice.name`:

```ts
export const store = configureStore({
  reducer: {
    [themeConfigSlice.name]: themeConfigSlice.reducer,
    [appApi.reducerPath]: appApi.reducer,
    [notificationsSlice.name]: notificationsSlice.reducer,
  },
  middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(rtkErrorMiddleware, appApi.middleware),
  devTools: process.env.NODE_ENV !== 'production',
})
```

`RootState` and `AppDispatch` are inferred from the store, so a new slice is immediately typed.

Reducers must stay **deterministic and side-effect free**. Persistence and DOM work (writing
`localStorage`, toggling the `dark` class on `<html>`) happen in `App.tsx` / provider components
reacting to state — `themeConfigSlice` is the model to follow. Use
`setLocalStorageValue` / `getLocalStorageValue` from `@/lib/utils` for that persistence.

## Reading and dispatching

Always the typed hooks from `@/store/hooks`, never bare `useSelector`/`useDispatch`:

```ts
const authState = useAppSelector((state) => state.auth)
const unread = useAppSelector((state) => state.notifications.unreadCount)
const isRTL = useAppSelector((state) => state.theme.rtlClass) === 'rtl'

const dispatch = useAppDispatch()
dispatch(setUnreadCount(count))
```

Select the narrowest slice of state a component needs so unrelated updates do not re-render it.

## Auth state

`authSlice` is filled from `/account/get-info` after sign-in and cleared by `signout()`. The RTK
Query `baseQueryWithReauth` dispatches `signout()` itself when a token refresh fails, then redirects
to `/signin?redirect=…` if the current path requires auth. Permission checks read this slice
through `isAllowed(authState, [Allow.X])` — see the `permissions` skill.

## Middleware

`store/middlewares/rtk-error-middleware.ts` inspects every rejected RTK Query action and dispatches
`showServiceUnavailable()` on a `FETCH_ERROR` (the API is unreachable), which renders
`ServiceUnavailableView`. Add cross-cutting reactions to API outcomes there rather than repeating
them in components:

```ts
export const rtkErrorMiddleware: Middleware = (api) => (next) => (action: unknown) => {
  if (isRejectedWithValue(action)) { /* inspect payload/status, dispatch */ }
  return next(action)
}
```

Export new middleware from `store/middlewares/index.ts` and add it to the `concat(...)` chain
**before** `appApi.middleware`.

## Checklist

- [ ] The data is genuinely client-owned (otherwise use RTK Query)
- [ ] Slice file + export from `store/slices/index.ts`
- [ ] Registered in `store/index.tsx` by `slice.name`
- [ ] Reducers side-effect free; persistence handled outside
- [ ] Consumers use `useAppSelector` / `useAppDispatch` with narrow selectors
