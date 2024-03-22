import { appApi } from "./_appApi";

type SignInInput = {
  username: string
  password: string
}

export type SignInDto = {
  token: string
}

const authApi = appApi.injectEndpoints({
  overrideExisting: false,
  endpoints: (builder => ({
    signIn: builder.mutation<SignInDto, SignInInput>({
      query: (input) => ({
        url: "/auth/login",
        method: "POST",
        body: input
      }),
      // transformErrorResponse: (error) => {
      //   return {
      //     ...error,
      //     meta: {
      //       ignoreStatuses: [422],
      //     },
      //   };
      // },
    })
  }))
})

export const { useSignInMutation } = authApi
