import { GetUserInfoResponse } from '@/store/api/identity'

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
 * A platform account is held to the same rule: it enters only a tenant it is a
 * member of, so the tenant it is acting in is among the tenants listed for it
 * exactly as it is for anybody else.
 */
export const isActiveTenantStale = (user: GetUserInfoResponse | undefined): boolean => {
  if (!user) return false

  const selectedTenantId = user.activeTenantId ?? user.activeTenant?.id
  if (!selectedTenantId) return false

  const isSelectable = (user.tenants ?? []).some((tenant) => tenant.id === selectedTenantId)
  return !isSelectable || user.activeTenant?.id !== selectedTenantId
}

/**
 * Returns true when an ordinary account is signed in with no tenant it may still act in - the tenant
 * its session named was suspended, deleted or left it, so the last renewal dropped it, or the selection
 * it still carries has gone stale. Such a session can open no tenant-scoped screen on this host, so the
 * route guard sends it to the workspaces list, which links to the tenants that are still its own (each
 * at its own address) and says so plainly when there are none. It never signs the account out: losing
 * a tenant is a reason to point at another, not to end the session.
 *
 * Signing in never needs this: the host names the tenant, so an ordinary account arrives already acting
 * in it or is refused. A platform account is never without one, since its own authority lives in
 * platform scope.
 */
export const isWithoutUsableTenant = (user: GetUserInfoResponse | undefined): boolean => {
  if (!user || user.isPlatform) return false
  return !user.activeTenant || isActiveTenantStale(user)
}

/**
 * The screens under `/admin` a platform administrator acting in no tenant can use, besides the
 * dashboard itself: users, roles and notifications answer there about the platform's own - platform
 * users, platform roles, notifications belonging to no tenant - the tenancy screens read no tenant
 * data, and localization answers about the platform's own texts and languages
 * (its permission is scoped `Both`, exactly like the screen), as do settings, which answer about the
 * platform's own overrides every tenant inherits. Any feature added later stays
 * tenant-only until it is listed here.
 */
const platformAccessiblePathPrefixes = [
  '/admin/users',
  '/admin/roles',
  '/admin/notifications',
  // efn:examples:start
  '/admin/ui',
  // efn:examples:end
  '/admin/editions',
  '/admin/localization',
  '/admin/settings',
  platformScopedPathPrefix,
]

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
const platformOnlyPathPrefixes = ['/admin/tenants/create', '/admin/editions']

/**
 * The platform's own screens that sit at the root of a section whose deeper screens are not all
 * platform-only, so they match exactly rather than as a prefix: the tenants list is `/admin/tenants`,
 * while a tenant's detail and members beneath it stay open inside that tenant.
 */
const platformOnlyPaths = ['/admin/tenants']

/**
 * The platform's own screens about one record, which a prefix cannot name because the record's id
 * comes before the screen: editing a tenant's entitlements is a platform act, not something a tenant
 * does to itself. `{id}` stands for exactly one path segment, as it does in `auth-urls.ts`.
 */
const platformOnlyPathPatterns = ['/admin/tenants/{id}/features'].map(
  (pattern) => new RegExp(`^${pattern.replace(/\{[^}]+\}/g, '[^/]+')}(/|$)`),
)

/**
 * Returns true when the (locale-stripped) path names one of the platform's own screens, which a
 * caller acting inside a tenant cannot open.
 */
export const isPlatformOnlyPath = (pathname: string): boolean => {
  const path = normalizePath(pathname)
  return (
    platformOnlyPaths.includes(path) ||
    platformOnlyPathPrefixes.some((prefix) => path === prefix || path.startsWith(`${prefix}/`)) ||
    platformOnlyPathPatterns.some((pattern) => pattern.test(path))
  )
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
 * land a caller who has followed a link into the wrong scope.
 */
export const isPathAvailable = (user: GetUserInfoResponse | undefined, pathname: string): boolean => {
  if (isPlatformWithoutTenant(user)) {
    return !isTenantScopedPath(pathname) || isPlatformAccessiblePath(pathname)
  }
  return !user?.activeTenant || !isPlatformOnlyPath(pathname)
}

/**
 * Decides where a platform account acting in no tenant goes after signing in:
 * the dashboard, rather than the workspaces list, because it
 * needs no tenant. A `redirect` it was sent back with is honoured when it can
 * use that screen. Returns `null` for anyone else, who arrives already acting
 * in the tenant the host names.
 */
export const resolvePlatformLanding = (
  user: GetUserInfoResponse | undefined,
  redirectTo?: string | null,
): string | null => {
  if (!isPlatformWithoutTenant(user)) return null
  if (redirectTo && isPathAvailable(user, redirectTo)) return redirectTo
  return '/admin'
}
