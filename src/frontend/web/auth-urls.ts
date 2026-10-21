import { Allow } from './allow'
// The helper module rather than the `@/i18n` barrel, whose client half reaches `components/layouts` - which imports this file.
import { withoutLocale } from './i18n/locale-path'

/** Pairing of a route pattern with the permissions required to access it (used by route guards and the proxy). */
export interface AuthUrl {
  url: string
  permissions?: string[]
}

/** Registry of routes the app considers "guarded", each annotated with the permissions required to access it. */
export const authUrls: AuthUrl[] = [
  {
    url: '/change-password',
  },
  {
    url: '/profile',
  },
  {
    url: '/',
  },
  {
    url: '/users',
    permissions: [Allow.User_View],
  },
  {
    url: '/users/create',
    permissions: [Allow.User_Create],
  },
  {
    url: '/users/{id}/update',
    permissions: [Allow.User_Update],
  },
  {
    url: '/roles',
    permissions: [Allow.Role_View],
  },
  {
    url: '/roles/create',
    permissions: [Allow.Role_Create],
  },
  {
    url: '/roles/{id}/update',
    permissions: [Allow.Role_Update],
  },
  {
    url: '/roles/{id}/change-permissions',
    permissions: [Allow.Role_ChangePermissions],
  },
  {
    // The tenants list is the platform's own screen - it answers about every tenant there is - so it
    // is gated on Tenant.View, which the API declares platform-scoped: a session carries it only
    // while acting in no tenant, so a tenant administrator never holds it however their roles are
    // granted. They reach their own tenant through the detail screen below instead.
    url: '/tenants',
    permissions: [Allow.Tenant_View],
  },
  {
    url: '/tenants/create',
    permissions: [Allow.Tenant_Create],
  },
  {
    // Editing a tenant's entitlements is a platform act, not something a tenant does to itself, so
    // every screen below is gated on a permission the API declares platform-scoped.
    url: '/tenants/{id}/features',
    permissions: [Allow.FeatureValue_View],
  },
  {
    url: '/editions',
    permissions: [Allow.Edition_View],
  },
  {
    url: '/editions/create',
    permissions: [Allow.Edition_Create],
  },
  {
    url: '/editions/{id}/update',
    permissions: [Allow.Edition_Update],
  },
  {
    url: '/editions/{id}/features',
    permissions: [Allow.FeatureValue_View],
  },
  {
    url: '/tenants/{id}/update',
    permissions: [Allow.Tenant_Update],
  },
  {
    url: '/tenants/{id}/members',
    permissions: [Allow.TenantMember_View],
  },
  {
    url: '/tenants/{id}/detail',
    permissions: [Allow.Tenant_Detail],
  },
  {
    url: '/select-tenant',
  },
  {
    url: '/localization',
    permissions: [Allow.Localization_View],
  },
  {
    // Both-scoped like localization: the platform edits the values every tenant inherits, a tenant
    // administrator its own tenant's overrides, on the same route and permission.
    url: '/settings',
    permissions: [Allow.Settings_View],
  },
]

/**
 * Finds the single auth-guarded route definition that matches the given
 * URL, supporting dynamic {id} segments via regex. Throws if more than
 * one definition matches the same pathname.
 */
export const getMatchedAuthUrl = (url: string): AuthUrl | undefined => {
  const pathname = url.split('?')[0]
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
    throw new Error(
      `Multiple auth URLs matched for: ${url}. Matches: ${matches.map((m) => m.url).join(', ')}`,
    )
  }

  if (matches.length === 0) {
    return undefined
  }

  return matches[0]
}

/**
 * The screens a visitor with no session may open: signing in or up, recovering a password, verifying
 * an email, redeeming a sign-in ticket, and the refusal screen. Each names its whole subtree.
 */
export const anonymousUrls = ['/signin', '/signup', '/forget-password', '/reset-password', '/verify-email', '/ticket-signin', '/unauthorized']

/** Returns true if the given URL, with or without its locale segment, names one of the anonymous screens or a path beneath one. */
export const isAnonymousUrl = (url: string) => {
  const pathname = withoutLocale(url.split('?')[0].split('#')[0])
  return anonymousUrls.some((anonymousUrl) => pathname === anonymousUrl || pathname.startsWith(`${anonymousUrl}/`))
}

/**
 * Returns true if the given URL needs a signed-in caller. The app has no public
 * pages, so everything does except the anonymous screens - a new route is guarded by default rather
 * than only once someone remembers to register it.
 */
export const isAuthRequired = (url: string) => {
  return !isAnonymousUrl(url)
}
