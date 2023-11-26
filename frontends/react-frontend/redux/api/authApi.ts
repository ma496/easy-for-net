import { appApi } from "./_appApi";

type SignInInput = {
  username: string
  password: string
}

const userApi = appApi.injectEndpoints({
  overrideExisting: false,
  endpoints: (builder => ({
    signIn: builder.mutation<string, SignInInput>({
      query: (input) => ({
        url: "Account/SignIn",
        method: "POST",
        body: input
      })
    })
  }))
})

export const { useSignInMutation } = userApi
