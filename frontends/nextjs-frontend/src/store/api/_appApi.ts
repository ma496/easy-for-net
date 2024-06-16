import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react'

export const appApi = createApi({
  reducerPath: 'appApi',
  baseQuery: fetchBaseQuery({
    baseUrl: 'https://localhost:5224/api/',
    prepareHeaders: (headers, api) => {
      // you can send jwt token to header
      return headers
    },
  }),
  endpoints: () => ({}),
})

