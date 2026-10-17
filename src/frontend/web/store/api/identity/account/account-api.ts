import { appApi } from '@/store/api/_app-api'
import {
  ChangePasswordRequest,
  TokenRequest,
  TokenResponse,
  GetUserInfoResponse,
  GetUserProfileResponse,
  RefreshTokenResponse,
  RefreshTokenRequest,
  UpdateProfileResponse,
  ForgetPasswordRequest,
  ResetPasswordRequest,
  UpdateProfileRequest,
  SignupRequest,
  VerifyEmailRequest,
  SignupResponse,
  ResendVerifyEmailRequest,
  SignInTicketRequest,
  SignInTicketResponse,
  SignInTicketRedeemRequest,
} from './account-dtos'

/**
 * RTK Query API for account-related endpoints: login, signup, email
 * verification, password change/reset, refresh token, fetching and
 * updating the current user profile, and signing out.
 */
export const accountApi = appApi.injectEndpoints({
  overrideExisting: false,
  endpoints: (builder) => ({
    token: builder.mutation<TokenResponse, TokenRequest>({
      query: (input) => ({
        url: '/account/token',
        method: 'POST',
        body: input,
      }),
    }),
    // Issued here, redeemed on the tenant's own address: each host keeps its own session.
    signInTicket: builder.mutation<SignInTicketResponse, SignInTicketRequest>({
      query: (input) => ({
        url: '/account/sign-in-ticket',
        method: 'POST',
        body: input,
      }),
    }),
    redeemSignInTicket: builder.mutation<TokenResponse, SignInTicketRedeemRequest>({
      query: (input) => ({
        url: '/account/sign-in-ticket/redeem',
        method: 'POST',
        body: input,
      }),
    }),
    signup: builder.mutation<SignupResponse, SignupRequest>({
      query: (input) => ({
        url: '/account/signup',
        method: 'POST',
        body: input,
      }),
    }),
    verifyEmail: builder.mutation<void, VerifyEmailRequest>({
      query: (input) => ({
        url: '/account/verify-email',
        method: 'POST',
        body: input,
      }),
    }),
    resendVerifyEmail: builder.mutation<void, ResendVerifyEmailRequest>({
      query: (input) => ({
        url: '/account/resend-verify-email',
        method: 'POST',
        body: input,
      }),
    }),
    changePassword: builder.mutation<void, ChangePasswordRequest>({
      query: (input) => ({
        url: '/account/change-password',
        method: 'POST',
        body: input,
      }),
    }),
    forgetPassword: builder.mutation<void, ForgetPasswordRequest>({
      query: (input) => ({
        url: '/account/forget-password',
        method: 'POST',
        body: input,
      }),
    }),
    resetPassword: builder.mutation<void, ResetPasswordRequest>({
      query: (input) => ({
        url: '/account/reset-password',
        method: 'POST',
        body: input,
      }),
    }),
    getUserInfo: builder.query<GetUserInfoResponse, void>({
      query: () => ({
        url: '/account/get-info',
        method: 'GET',
      }),
    }),
    getUserProfile: builder.query<GetUserProfileResponse, void>({
      query: () => ({
        url: '/account/profile',
        method: 'GET',
      }),
    }),
    refreshToken: builder.mutation<RefreshTokenResponse, RefreshTokenRequest>({
      query: (input) => ({
        url: '/account/refresh-token',
        method: 'POST',
        body: input,
      }),
    }),
    updateProfile: builder.mutation<UpdateProfileResponse, UpdateProfileRequest>({
      query: (input) => ({
        url: '/account/update-profile',
        method: 'POST',
        body: input,
      }),
    }),
    signout: builder.mutation<void, void>({
      query: () => ({
        url: '/account/signout',
        method: 'POST',
      }),
    }),
  }),
})

export const {
  useTokenMutation,
  useSignInTicketMutation,
  useRedeemSignInTicketMutation,
  useSignupMutation,
  useVerifyEmailMutation,
  useResendVerifyEmailMutation,
  useChangePasswordMutation,
  useForgetPasswordMutation,
  useResetPasswordMutation,
  useGetUserInfoQuery,
  useLazyGetUserInfoQuery,
  useGetUserProfileQuery,
  useRefreshTokenMutation,
  useUpdateProfileMutation,
  useSignoutMutation,
} = accountApi
