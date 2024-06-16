import { configureStore } from '@reduxjs/toolkit'
import { appApi } from './api/_appApi'
import { themeConfigSlice } from './slices/themeConfigSlice'

export const store = configureStore({
  reducer: {
    [appApi.reducerPath]: appApi.reducer,
    [themeConfigSlice.name]: themeConfigSlice.reducer
  },
  middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(appApi.middleware), // you can add custom middleware
  devTools: process.env.NODE_ENV !== "production",
})

export type RootState = ReturnType<typeof store.getState>
export type AppDispatch = typeof store.dispatch
