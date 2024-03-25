import { constants } from '@/lib/constants'
import { getLocalStorageValue } from '@/lib/utils'
import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react'
import { SignInDto } from './authApi'

export const appApi = createApi({
  reducerPath: 'appApi',
  baseQuery: fetchBaseQuery({
    baseUrl: 'https://localhost:5224/api/',
    prepareHeaders: (headers, api) => {
      if (api.endpoint === 'signIn') return headers
      const loginInfo = getLocalStorageValue<SignInDto>(constants.localStorage.login)
      if (loginInfo && loginInfo.accessToken) {
        headers.set('authorization', `Bearer ${loginInfo.accessToken}`)
      }
      return headers
    },
  }),
  endpoints: () => ({}),
})

