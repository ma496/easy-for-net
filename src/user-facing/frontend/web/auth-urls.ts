import { Allow } from './allow'
// The helper modules rather than the `@/i18n` barrel, whose client half reaches `components/layouts` - which imports this file.
import { withoutLocale } from './i18n/locale-path'
import { accountPath, adminPath, isAccountPath, isAdminPath } from './lib/paths'

/** Pairing of a route pattern with the permissions required to access it (used by route guards and the proxy). */
export interface AuthUrl {
  url: string
  permissions?: string[]
}

/**
 * Registry of the guarded screens, each annotated with the permissions required to open it beyond a
 * session. Every admin screen also needs `Admin.Access` - the area's own gate - so it is written into
 * each admin entry by {@link admin} rather than repeated by hand.
 */
const admin = (path: string, ...permissions: string[]): AuthUrl => ({ url: adminPath(path), permissions: [Allow.Admin_Access, ...permissions] })

export const authUrls: AuthUrl[] = [
  { url: accountPath('/profile') },
  { url: accountPath('/change-password') },
  admin('/'),
  admin('/users', Allow.User_View),
  admin('/users/create', Allow.User_Create),
  admin('/users/{id}/update', Allow.User_Update),
  admin('/roles', Allow.Role_View),
  admin('/roles/create', Allow.Role_Create),
  admin('/roles/{id}/update', Allow.Role_Update),
  admin('/roles/{id}/change-permissions', Allow.Role_ChangePermissions),
  admin('/localization', Allow.Localization_View),
  admin('/settings', Allow.Settings_View),
  admin('/seo', Allow.Seo_View),
  admin('/seo/create', Allow.Seo_Update),
  admin('/seo/{id}/update', Allow.Seo_Update),
]

/**
 * The permissions a locale-free admin path needs when no entry above names it - a new screen, the
 * notifications, the component examples: the area's own gate. Outside the admin area, nothing.
 */
const fallbackPermissions = (pathname: string): string[] | undefined => (isAdminPath(pathname) ? [Allow.Admin_Access] : undefined)

/**
 * Finds the single auth-guarded route definition that matches the given
 * URL, supporting dynamic {id} segments via regex. Throws if more than
 * one definition matches the same pathname. An admin path no entry names
 * still answers with the area's own gate, `Admin.Access`.
 */
export const getMatchedAuthUrl = (url: string): AuthUrl | undefined => {
  const pathname = url.split('?')[0].split('#')[0]
  const matches = authUrls.filter((authUrl) => {
    if (authUrl.url === pathname) {
      return true
    }

    // Handle dynamic segments like {id}
    if (authUrl.url.includes('{')) {
      const pattern = authUrl.url.replace(/\{[^}]+\}/g, '[^/]+')
      const regex = new RegExp(`^${pattern}$`)
      return regex.test(pathname)
    }

    return false
  })

  if (matches.length > 1) {
    throw new Error(`Multiple auth URLs matched for: ${url}. Matches: ${matches.map((m) => m.url).join(', ')}`)
  }

  if (matches.length === 0) {
    const permissions = fallbackPermissions(pathname)
    return permissions ? { url: pathname, permissions } : undefined
  }

  return matches[0]
}

/**
 * The screens that only make sense without a session: signing in or up, recovering a password and
 * verifying an email. Each names its whole subtree. They are public like every other page; the list
 * exists so a signed-in visitor can be told apart from one who is not.
 */
export const anonymousUrls = ['/signin', '/signup', '/forget-password', '/reset-password', '/verify-email', '/unauthorized']

/** Returns true if the given URL, with or without its locale segment, names one of the anonymous screens or a path beneath one. */
export const isAnonymousUrl = (url: string) => {
  const pathname = withoutLocale(url.split('?')[0].split('#')[0])
  return anonymousUrls.some((anonymousUrl) => pathname === anonymousUrl || pathname.startsWith(`${anonymousUrl}/`))
}

/**
 * Returns true if the given URL, with or without its locale segment, needs a signed-in caller: the
 * admin area and the visitor's own account screens. Every other page - the public site and the
 * anonymous screens - is open, so a crawler and a first-time visitor see its real content.
 */
export const isAuthRequired = (url: string) => {
  const pathname = withoutLocale(url.split('?')[0].split('#')[0])
  return isAdminPath(pathname) || isAccountPath(pathname)
}
