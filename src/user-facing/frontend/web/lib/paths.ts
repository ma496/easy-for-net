/** The URL segment every administration screen lives under. */
export const ADMIN_BASE = '/admin'

/** The URL segment the signed-in visitor's own account screens live under. */
export const ACCOUNT_BASE = '/account'

/** Joins a base segment and a locale-free path, so `'/'` names the base itself and a trailing slash never appears. */
const under = (base: string, path: string): string => {
  const normalized = path.startsWith('/') ? path : `/${path}`
  return normalized === '/' ? base : `${base}${normalized}`
}

/**
 * The address of an administration screen: `adminPath('/users')` is `/admin/users`, `adminPath()` the
 * dashboard. Every admin link goes through here, so moving the admin area is a one-line change.
 */
export const adminPath = (path = '/'): string => under(ADMIN_BASE, path)

/** The address of one of the signed-in visitor's own account screens: `accountPath('/profile')` is `/account/profile`. */
export const accountPath = (path = '/'): string => under(ACCOUNT_BASE, path)

/** Whether the path is the segment itself or a path beneath it. */
const isUnder = (path: string, base: string): boolean => path === base || path.startsWith(`${base}/`)

/** Whether the locale-free path names the admin area or a screen beneath it. */
export const isAdminPath = (path: string): boolean => isUnder(path.split('?')[0].split('#')[0], ADMIN_BASE)

/** Whether the locale-free path names the account area or a screen beneath it. */
export const isAccountPath = (path: string): boolean => isUnder(path.split('?')[0].split('#')[0], ACCOUNT_BASE)
