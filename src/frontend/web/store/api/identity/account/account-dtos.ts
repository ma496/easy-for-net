import { RequestBase } from '@/store/api'
import { TenantStatus } from '@/store/api/tenancy/enums'

/** Request body for the change-password endpoint, supplying the current and desired new password. */
export interface ChangePasswordRequest extends RequestBase {
  currentPassword: string
  newPassword: string
}

/** Request body for initiating the password-reset flow, containing the user's email. */
export interface ForgetPasswordRequest extends RequestBase {
  email: string
}

/** Authenticated user info returned by /account/get-info, including the caller's tenants, the tenant they are acting in, and the roles and per-role permissions that tenant grants them. */
export interface GetUserInfoResponse {
  id: string
  username: string
  email: string
  firstName?: string
  lastName?: string
  image?: string
  activeTenantId?: string
  activeTenant?: GetUserInfoTenant
  tenants: GetUserInfoTenant[]
  isPlatform: boolean
  roles: GetUserInfoRole[]
  /** The platform origin, which a platform account opens to work in platform scope. */
  platformUrl: string
}

/** Role summary embedded in GetUserInfoResponse, listing the role's permissions. */
export interface GetUserInfoRole {
  id: string
  name: string
  permissions: GetUserInfoPermission[]
}

/** Permission summary (id, name, displayName) embedded in GetUserInfoRole. */
export interface GetUserInfoPermission {
  id: string
  name: string
  displayName: string
}

/** A tenant the caller holds an active membership in, embedded in GetUserInfoResponse so the user menu can link to the other workspaces at their own addresses. */
export interface GetUserInfoTenant {
  id: string
  name: string
  identifier: string
  status: TenantStatus
  /** The tenant's own origin, where its sign-in and session live; null when host resolution is off. */
  url: string | null
}

/** Public-facing profile of the current user returned by /account/profile (no roles/permissions). */
export interface GetUserProfileResponse {
  id: string
  username: string
  email: string
  firstName?: string
  lastName?: string
  image?: string
}

/** Request body for the refresh-token endpoint, pairing the user id with the current refresh token. */
export interface RefreshTokenRequest extends RequestBase {
  userId: string
  refreshToken: string
}

/** Response from the refresh-token endpoint, returning the freshly issued access/refresh tokens. */
export interface RefreshTokenResponse {
  userId: string
  accessToken: string
  refreshToken: string
}

/** Request body for the resend-verify-email endpoint, identifying the user by email or username. */
export interface ResendVerifyEmailRequest extends RequestBase {
  emailOrUsername: string
}

/** Request body for the reset-password endpoint, combining a one-time token with the new password. */
export interface ResetPasswordRequest extends RequestBase {
  token: string
  password: string
}

/** Request body for the signup endpoint, providing the new account credentials and the tenant created alongside the account. */
export interface SignupRequest extends RequestBase {
  username: string
  email: string
  password: string
  confirmPassword: string
  tenantName: string
  tenantIdentifier: string
}

/** Response from the signup endpoint, indicating whether email verification must be completed before login. */
export interface SignupResponse {
  isEmailVerificationRequired: boolean
}

/** Request body for the login (/account/token) endpoint, supplying username and password; the request's host decides the tenant. */
export interface TokenRequest extends RequestBase {
  username: string
  isEmail?: boolean
  email?: string
  password: string
}

/** Response from the login endpoint, returning the issued access/refresh tokens and the user id. */
export interface TokenResponse {
  accessToken: string
  userId: string
  refreshToken: string
}

/** Request body for updating the current user's profile (email, name, image). */
export interface UpdateProfileRequest extends RequestBase {
  email: string
  firstName: string | undefined
  lastName: string | undefined
  image: string | undefined
}

/** Response from the update-profile endpoint, returning the persisted profile fields. */
export interface UpdateProfileResponse {
  id: string
  email: string
  firstName?: string
  lastName?: string
  image?: string
}

/** Request body for the verify-email endpoint, carrying the verification token delivered to the user. */
export interface VerifyEmailRequest extends RequestBase {
  token: string
}
