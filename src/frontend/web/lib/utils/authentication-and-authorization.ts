import { GetUserInfoResponse } from '@/store/api/identity'

/** Authentication slice shape: the current user (with roles/permissions) and an isAuthenticated flag. */
export interface AuthState {
  user: GetUserInfoResponse | undefined
  isAuthenticated: boolean
}

/**
 * Returns true if the authenticated state grants all the listed
 * permission names, matching any of the user's roles. An empty
 * permissions list is treated as "no permission required".
 */
export const isAllowed = (state: AuthState, permissions: string[]): boolean => {
  if (!state.user || !state.user.roles) return false
  if (!permissions || permissions.length === 0) return true

  // Check if any of the user's role permissions match the required permissions
  return state.user.roles.some((role) => role.permissions?.some((permission) => permissions.includes(permission.name)))
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
