import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react'
import { environment } from '@/config'
import { Mutex } from 'async-mutex'
import { BaseQueryFn, FetchArgs, FetchBaseQueryError } from '@reduxjs/toolkit/query'
import { setUserInfo, signout } from '../slices/authSlice'
import { isAuthRequired } from '@/auth-urls'
// Imported straight from the DTO file rather than through the feature barrel, and as a type alone,
// so this module gains no runtime edge back to the feature APIs that are injected into it.
import type { GetUserInfoResponse } from './identity/account/account-dtos'

const mutex = new Mutex()

const baseQuery = fetchBaseQuery({
  baseUrl: environment.apiUrl,
  prepareHeaders: (headers) => {
    return headers
  },
  credentials: 'include',
})

const baseQueryWithReauth: BaseQueryFn<
  string | FetchArgs,
  unknown,
  FetchBaseQueryError
> = async (args, api, extraOptions) => {
  // wait until the mutex is available without locking it
  await mutex.waitForUnlock()
  let result = await baseQuery(args, api, extraOptions)

  if (result.error && (result.error.status === 401 || result.error.status === 404)) {
    const isUserInfoEndpoint = (
      typeof args === 'string' ? args : args.url
    )?.includes('/account/get-info')

    if (result.error.status === 404 && !isUserInfoEndpoint) {
      return result
    }
    if (!mutex.isLocked()) {
      const release = await mutex.acquire()
      try {
        const refreshResult = await baseQuery(
          {
            url: '/account/refresh-token',
            method: 'POST',
            body: {
              // The backend will read from the cookie if refreshToken is missing
              // We can include userId if available in the state, but let's try relying on the cookie + updated logic
              // However, the backend logic I wrote earlier checks `httpContext.User` which might be unauthenticated if 401.
              // So, ideally, we should pass the userId.
              // eslint-disable-next-line @typescript-eslint/no-explicit-any
              userId: (api.getState() as any).auth?.user?.id ?? ''
            },
          },
          api,
          extraOptions
        )

        if (refreshResult.data) {
          // Retry the initial query
          result = await baseQuery(args, api, extraOptions)

          // A renewal re-reads what the caller may do, so the session it hands back is not always the
          // one that expired: a tenant that has since been suspended, deleted or left is dropped, and
          // the renewed session then holds no permission at all. Re-reading the account info here is
          // what lets the route guard notice and offer the caller another tenant - without it the app
          // would go on believing in the tenant it lost and show refusals it cannot explain.
          // Taken through the plain base query rather than this one, so a 401 on the read cannot
          // re-enter the refresh it was triggered by. A read that fails leaves the previous info
          // standing and is not retried here: the app re-reads it on its next mount, and the
          // alternative - discarding what we know on a network blip - would throw the caller out of a
          // tenant that never went anywhere.
          const userInfo = await baseQuery('/account/get-info', api, extraOptions)
          if (userInfo.data) {
            api.dispatch(setUserInfo(userInfo.data as GetUserInfoResponse))
          }
        } else {
          api.dispatch(signout())
          const pathname = typeof window !== 'undefined' ? window.location.pathname : undefined
          if (pathname && isAuthRequired(pathname)) {
            const signinUrl = new URL('/signin', window.location.origin)
            signinUrl.searchParams.set('redirect', pathname)
            window.location.href = signinUrl.toString()
          }
        }
      } finally {
        release()
      }
    } else {
      // wait until the mutex is available without locking it
      await mutex.waitForUnlock()
      result = await baseQuery(args, api, extraOptions)
    }
  }
  return result
}

/**
 * Base RTK Query API instance used as the root for all feature APIs
 * (account, users, roles, files, notifications, etc.). Wraps a
 * fetchBaseQuery with a reauth flow that refreshes the access token on
 * 401/404 and signs the user out / redirects on failure.
 */
export const appApi = createApi({
  reducerPath: 'appApi',
  baseQuery: baseQueryWithReauth,
  endpoints: () => ({}),
})
