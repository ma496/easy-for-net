import { GetUserInfoResponse, GetUserInfoTenant } from '@/store/api/identity'

/**
 * Authentication slice shape: the current user (with the roles and permissions granted
 * in the tenant being acted in), the active tenant, every tenant the user may act in,
 * the last tenant failure code reported by the API, and an isAuthenticated flag.
 * activeTenant is undefined while the user has not chosen a tenant yet, and tenants is
 * empty for an account that holds no usable membership.
 */
export interface AuthState {
  user: GetUserInfoResponse | undefined
  isAuthenticated: boolean
  activeTenant: GetUserInfoTenant | undefined
  tenants: GetUserInfoTenant[]
  tenantError: string | undefined
}

/**
 * Returns true if the authenticated state grants all the listed
 * permission names, matching any of the user's roles. An empty
 * permissions list is treated as "no permission required".
 *
 * The roles held in state are the ones the API returns for the tenant the
 * user is acting in, together with their platform-scoped roles, which belong
 * to no tenant and are granted in every one - the same set the API authorizes
 * a request from, so a screen is offered here exactly when the call behind it
 * would be allowed. The answer is already scoped to the active tenant and no
 * tenant argument is needed. The check deliberately does not require an
 * active tenant: platform-tier permissions must keep evaluating while the
 * user is acting in no tenant at all.
 */
export const isAllowed = (state: AuthState, permissions: string[]): boolean => {
  if (!state.user || !state.user.roles) return false
  if (!permissions || permissions.length === 0) return true

  const grantedPermissions = new Set(state.user.roles.flatMap((role) => role.permissions?.map((permission) => permission.name) ?? []))
  return permissions.every((permission) => grantedPermissions.has(permission))
}

// eslint-disable-next-line @typescript-eslint/no-require-imports
const cookieObj = typeof window === 'undefined' ? require('next/headers') : require('universal-cookie')

/**
 * Server-only check that determines whether the incoming request carries
 * an authentication cookie (the standard ASP.NET Core cookie or a
 * configured 'refreshToken' cookie). Always returns false in the browser.
 */
// Check for server-side HttpOnly cookie
export const hasAuthCookie = async () => {
  if (typeof window === 'undefined') {
    const cookies = await cookieObj.cookies()
    // Check for the standard ASP.NET Core cookie or configured name
    return cookies.has('.AspNetCore.Cookies') || cookies.has('refreshToken')
  }
  return false
}
