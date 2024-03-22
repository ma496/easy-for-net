import { appApi } from "./_appApi";

const checkoneApi = appApi.injectEndpoints({
  overrideExisting: false,
  endpoints: (builder => ({
    checkone: builder.query<any, any>({
      query: () => "checkone"
    })
  }))
})

export const { useCheckoneQuery } = checkoneApi
