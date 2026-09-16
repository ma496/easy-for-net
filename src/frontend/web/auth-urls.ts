import { Allow } from './allow'

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
    url: '/admin',
  },
  {
    url: '/admin/users/list',
    permissions: [Allow.User_View],
  },
  {
    url: '/admin/users/create',
    permissions: [Allow.User_Create],
  },
  {
    url: '/admin/users/update/{id}',
    permissions: [Allow.User_Update],
  },
  {
    url: '/admin/roles/list',
    permissions: [Allow.Role_View],
  },
  {
    url: '/admin/roles/create',
    permissions: [Allow.Role_Create],
  },
  {
    url: '/admin/roles/update/{id}',
    permissions: [Allow.Role_Update],
  },
  {
    url: '/admin/roles/change-permissions/{id}',
    permissions: [Allow.Role_ChangePermissions],
  },
  {
    url: '/admin/tenants/list',
    permissions: [Allow.Tenant_View],
  },
  {
    url: '/admin/tenants/create',
    permissions: [Allow.Tenant_Create],
  },
  {
    url: '/admin/tenants/update/{id}',
    permissions: [Allow.Tenant_Update],
  },
  {
    url: '/admin/tenants/members/{id}',
    permissions: [Allow.TenantMember_View],
  },
  {
    url: '/select-tenant',
  },
  {
    url: '/no-tenant',
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

/** Returns true if the given URL targets the /admin area or matches any entry in the authUrls registry. */
export const isAuthRequired = (url: string) => {
  return url.includes('/admin/') || !!getMatchedAuthUrl(url)
}
