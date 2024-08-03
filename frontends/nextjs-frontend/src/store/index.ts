import { configureStore } from '@reduxjs/toolkit'
import { appApi } from './api/_appApi'
import { themeConfigSlice } from './slices/themeConfigSlice'
import { errorSlice } from './slices/errorSlice'
import { rtkErrorHandler } from './middlewares'

export const store = configureStore({
  reducer: {
    [appApi.reducerPath]: appApi.reducer,
    [themeConfigSlice.name]: themeConfigSlice.reducer,
    [errorSlice.name]: errorSlice.reducer
  },
  middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(rtkErrorHandler, appApi.middleware), // you can add custom middleware
  devTools: process.env.NODE_ENV !== "production",
})

export type RootState = ReturnType<typeof store.getState>
export type AppDispatch = typeof store.dispatch
