import { appApi } from "./_appApi";

const userApi = appApi.injectEndpoints({
  overrideExisting: false,
  endpoints: (builder => ({
    getWeater: builder.query<any, any>({
      query: () => "WeatherForecast"
    })
  }))
})

export const { useGetWeaterQuery } = userApi
