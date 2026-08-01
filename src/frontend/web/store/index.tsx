import { configureStore } from '@reduxjs/toolkit'
import { appApi } from '@/store/api/_app-api'
import { themeConfigSlice, authSlice, notificationsSlice } from '@/store/slices'
import { rtkErrorMiddleware } from '@/store/middlewares'

/**
 * Root Redux store combining the theme config, auth, and notifications
 * slices with the RTK Query appApi. Adds the custom rtkErrorMiddleware
 * to the default middleware chain to surface rejected API errors.
 */
export const store = configureStore({
  reducer: {
    [themeConfigSlice.name]: themeConfigSlice.reducer,
    [appApi.reducerPath]: appApi.reducer,
    [authSlice.name]: authSlice.reducer,
    [notificationsSlice.name]: notificationsSlice.reducer,
  },
  middleware: (getDefaultMiddleware) =>
    getDefaultMiddleware({
      serializableCheck: false,
    }).concat(rtkErrorMiddleware, appApi.middleware),
  devTools: process.env.NODE_ENV !== 'production',
})

export type RootState = ReturnType<typeof store.getState>
export type AppDispatch = typeof store.dispatch
