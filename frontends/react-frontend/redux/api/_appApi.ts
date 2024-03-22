import { constants } from '@/lib/constants'
import { getLocalStorageValue } from '@/lib/utils'
import { createApi, fetchBaseQuery, FetchBaseQueryError } from '@reduxjs/toolkit/query/react'
import { SignInDto } from './authApi'

export const appApi = createApi({
  reducerPath: 'appApi',
  baseQuery: fetchBaseQuery({
    baseUrl: 'https://localhost:5224/api/',
    prepareHeaders: (headers, api) => {
      if (api.endpoint === 'signIn') return headers
      const loginInfo = getLocalStorageValue<SignInDto>(constants.localStorage.login)
      if (loginInfo && loginInfo.token) {
        headers.set('authorization', `Bearer ${loginInfo.token}`)
      }
      return headers
    },
  }),
  endpoints: () => ({}),
})
