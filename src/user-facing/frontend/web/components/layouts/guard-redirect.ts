import { getMatchedAuthUrl } from '@/auth-urls'

/**
 * Where a guarded screen sends the visitor, decided on the locale-free path: nowhere while the account
 * info has not answered, to sign in (returning here) when it answered with no session, to
 * `/unauthorized` when the session lacks a permission the screen needs - `Admin.Access` for every admin
 * screen - and nowhere when it may open it. Pure, so the rule is testable without a router.
 */
export const decideGuardRedirect = (input: {
  path: string
  isSettled: boolean
  isAuthenticated: boolean
  isPermitted: (permissions: string[]) => boolean
}): string | null => {
  if (!input.isSettled) return null
  if (!input.isAuthenticated) {
    return `/signin?redirect=${encodeURIComponent(input.path)}`
  }
  const permissions = getMatchedAuthUrl(input.path)?.permissions
  if (permissions && permissions.length > 0 && !input.isPermitted(permissions)) {
    return '/unauthorized'
  }
  return null
}
