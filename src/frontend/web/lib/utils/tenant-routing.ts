import { GetUserInfoResponse } from '@/store/api/identity'

/** The screen an authenticated caller is sent to while no usable tenant selection stands: the chooser, which offers the tenants they may work in and a way out when there are none. */
export type TenantLandingRoute = '/select-tenant'

/** Paths outside `/admin` are never tenant-scoped, and these ones under it are the platform-tier tenancy screens, which a platform account works with while acting in no tenant at all. */
const platformScopedPathPrefix = '/admin/tenants'

/** Strips the query string, the fragment and a trailing slash so a path can be compared as a plain prefix. The locale prefix is expected to be gone already: callers read it off `usePathname` and remove it before asking. */
const normalizePath = (pathname: string): string => {
  const path = pathname.split('?')[0].split('#')[0]
  return path.length > 1 && path.endsWith('/') ? path.slice(0, -1) : path
}

/**
 * Returns true when the (locale-stripped) path names a screen that can only be
 * rendered while a tenant is active, so a caller without one must not be left on
 * it. Everything under `/admin` qualifies except the platform tenancy screens,
 * which are gated by platform permissions and stay reachable with no active
 * tenant. Account self-service (`/profile`, `/change-password`), the two landing
 * screens, `/unauthorized` and the public routes are therefore all tenant-free.
 */
export const isTenantScopedPath = (pathname: string): boolean => {
  const path = normalizePath(pathname)
  if (path !== '/admin' && !path.startsWith('/admin/')) return false
  return path !== platformScopedPathPrefix && !path.startsWith(`${platformScopedPathPrefix}/`)
}

/**
 * Returns true when the stored active-tenant selection names a tenant the caller
 * may no longer act in - it was suspended or deleted, or their membership in it
 * ended - so the selection has to be discarded and a new choice made rather than
 * kept and sent on. Those three cases all show up the same way: the selected id
 * is missing from the tenants the caller may work in, which the server reports
 * as the tenants they hold an active membership in that are themselves active.
 * A caller with no selection at all is not stale; there is nothing to discard.
 *
 * This is the mechanism rather than a belt-and-braces guard, and that is a change
 * from how it once worked. The account info endpoint reports the tenant the
 * session actually names, whether or not the caller still belongs to it, because
 * that is the tenant every other endpoint will act in for them until their
 * session is next renewed - hiding it there would have the screen and the API
 * disagree. So a membership that ended does reach the browser as a selection, and
 * this predicate is what recognises it: the selected id is missing from the
 * tenants the caller may work in. Do not simplify it away on the assumption that
 * the server filters it out.
 *
 * A platform account is exempt, because for it the two disagree by design: it
 * enters a tenant on its account tier and holds no membership in it, so the
 * tenant it is acting in is never among the tenants listed. Reading that as
 * stale would send it straight back out of the tenant it entered to look into a
 * problem.
 */
export const isActiveTenantStale = (user: GetUserInfoResponse | undefined): boolean => {
  if (!user) return false
  if (user.isPlatform) return false

  const selectedTenantId = user.activeTenantId ?? user.activeTenant?.id
  if (!selectedTenantId) return false

  const isSelectable = (user.tenants ?? []).some((tenant) => tenant.id === selectedTenantId)
  return !isSelectable || user.activeTenant?.id !== selectedTenantId
}

/**
 * Decides where an authenticated caller has to land before any tenant-scoped
 * screen may open, from the user info the server just answered with:
 *
 * - no usable selection - the tenant they were working in was suspended,
 *   deleted or left them, so their session was renewed without one - sends them
 *   to `/select-tenant` to choose again, and never signs them out: losing a
 *   tenant is a reason to offer them another, not to end the session;
 * - no tenant left to work in at all sends them to the same screen, which says
 *   so and offers them the way out. It is one screen rather than two because
 *   there is one thing to tell them: which tenants are theirs, and there may be
 *   none;
 * - a selection that still stands returns `null`, meaning the caller may go
 *   wherever they were headed.
 *
 * Signing in settles this for almost everybody: an ordinary account signs in to
 * exactly one tenant or is asked which one it meant, so it arrives here with a
 * selection already standing. What is left is a selection that stopped being
 * usable while the session was open.
 *
 * A platform account lands nowhere: it needs no tenant to work, it holds no
 * membership to be offered a choice from, and the tenant it enters is chosen
 * from the tenants table rather than from this screen - sent to the chooser it
 * would be shown an empty one.
 */
export const resolveTenantLanding = (user: GetUserInfoResponse | undefined): TenantLandingRoute | null => {
  if (!user) return null
  if (user.isPlatform) return null
  if (!user.activeTenant || isActiveTenantStale(user)) return '/select-tenant'
  return null
}

/**
 * The screens under `/admin` a platform administrator acting in no tenant can use, besides the
 * dashboard itself: users, roles and notifications answer there about the platform's own - platform
 * users, platform roles, notifications belonging to no tenant - and the UI showcase and the tenancy
 * screens read no tenant data. Any feature added later stays tenant-only until it is listed here.
 */
const platformAccessiblePathPrefixes = ['/admin/users', '/admin/roles', '/admin/notifications', '/admin/ui', '/admin/editions', platformScopedPathPrefix]

/**
 * Returns true when the (locale-stripped) path names a screen a platform
 * administrator can use while acting in no tenant.
 */
export const isPlatformAccessiblePath = (pathname: string): boolean => {
  const path = normalizePath(pathname)
  if (path === '/admin') return true
  return platformAccessiblePathPrefixes.some((prefix) => path === prefix || path.startsWith(`${prefix}/`))
}

/**
 * Returns true when the caller is a platform account acting in no tenant, whose
 * requests the API answers in platform scope - and whose session therefore
 * carries the platform-scoped permissions rather than a tenant's.
 */
export const isPlatformWithoutTenant = (user: GetUserInfoResponse | undefined): boolean =>
  !!user?.isPlatform && !user.activeTenant

/**
 * The screens under `/admin` that answer about every tenant there is, so they belong to platform
 * scope alone: listing the tenants and creating one. Reading a single tenant and administering its
 * members are not here, because those are exercisable in either scope - a tenant administrator opens
 * its own tenant's detail and members from inside it.
 */
const platformOnlyPathPrefixes = ['/admin/tenants/list', '/admin/tenants/create', '/admin/tenants/features', '/admin/editions']

/**
 * Returns true when the (locale-stripped) path names one of the platform's own screens, which a
 * caller acting inside a tenant cannot open.
 */
export const isPlatformOnlyPath = (pathname: string): boolean => {
  const path = normalizePath(pathname)
  return platformOnlyPathPrefixes.some((prefix) => path === prefix || path.startsWith(`${prefix}/`))
}

/**
 * Returns true when the caller may open the screen at this path. Two scopes, two
 * ways to be in the wrong one:
 *
 * - a platform account acting in no tenant cannot open a screen that needs one;
 * - anyone acting inside a tenant cannot open the platform's own screens, whose
 *   permissions are platform-scoped and so are never carried by a tenant
 *   session, however the caller's roles are granted.
 *
 * Neither is a caller short of a grant somebody could give them, so both are
 * answered with the dashboard rather than a refusal. Navigation and search use
 * this to leave out what would only redirect, and the route guard uses it to
 * land a caller whose scope has just changed under them - which is what entering
 * a tenant from the tenants table does.
 */
export const isPathAvailable = (user: GetUserInfoResponse | undefined, pathname: string): boolean => {
  if (isPlatformWithoutTenant(user)) {
    return !isTenantScopedPath(pathname) || isPlatformAccessiblePath(pathname)
  }
  return !user?.activeTenant || !isPlatformOnlyPath(pathname)
}

/**
 * Decides where a platform account acting in no tenant goes after signing in:
 * the dashboard, rather than the tenant chooser, because it
 * needs no tenant. A `redirect` it was sent back with is honoured when it can
 * use that screen. Returns `null` for anyone else, whose landing
 * `resolveTenantLanding` decides.
 */
export const resolvePlatformLanding = (
  user: GetUserInfoResponse | undefined,
  redirectTo?: string | null,
): string | null => {
  if (!isPlatformWithoutTenant(user)) return null
  if (redirectTo && isPathAvailable(user, redirectTo)) return redirectTo
  return '/admin'
}
