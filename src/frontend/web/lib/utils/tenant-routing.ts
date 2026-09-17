import { GetUserInfoResponse } from '@/store/api/identity'

/** The screens an authenticated caller is sent to while no usable tenant selection stands: the chooser when they may pick one, the no-tenant screen when they may not. */
export type TenantLandingRoute = '/select-tenant' | '/no-tenant'

/** Paths outside `/admin` are never tenant-scoped, and these ones under it are the platform-tier tenancy screens, which a platform administrator works with while acting in no tenant at all. */
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
 * This is a guard, not the mechanism AC-127 relies on: the account info endpoint
 * already reports the active tenant only when it is one of the tenants listed,
 * so a selection that has gone stale reaches the browser as no selection at all
 * and is caught by the `activeTenant` check in `resolveTenantLanding`. The test
 * plan asks for the predicate all the same, and it keeps the decision right if a
 * later server ever answers with the two disagreeing.
 */
export const isActiveTenantStale = (user: GetUserInfoResponse | undefined): boolean => {
  if (!user) return false

  const selectedTenantId = user.activeTenantId ?? user.activeTenant?.id
  if (!selectedTenantId) return false

  const isSelectable = (user.tenants ?? []).some((tenant) => tenant.id === selectedTenantId)
  return !isSelectable || user.activeTenant?.id !== selectedTenantId
}

/**
 * Decides where an authenticated caller has to land before any tenant-scoped
 * screen may open, from the user info the server just answered with:
 *
 * - no tenant they may work in - a self-service sign-up that has joined nothing,
 *   or an account whose last membership ended - sends them to `/no-tenant`, the
 *   screen that explains they belong to no active tenant and offers account
 *   self-service, creating their own tenant, and signing out;
 * - a tenant they may work in but no usable selection - several memberships and
 *   no choice made yet, or a choice that has gone stale - sends them to
 *   `/select-tenant` to choose, and never signs them out: the tenant they were
 *   using being suspended is a reason to offer them another, not to end the
 *   session;
 * - a selection that still stands returns `null`, meaning the caller may go
 *   wherever they were headed.
 *
 * The first case is checked first on purpose: a caller with no tenant at all and
 * a stale selection is sent to `/no-tenant`, never to a chooser with nothing to
 * choose from.
 */
export const resolveTenantLanding = (user: GetUserInfoResponse | undefined): TenantLandingRoute | null => {
  if (!user) return null
  if ((user.tenants ?? []).length === 0) return '/no-tenant'
  if (!user.activeTenant || isActiveTenantStale(user)) return '/select-tenant'
  return null
}

/**
 * The screens under `/admin` a platform administrator acting in no tenant can use, besides the
 * dashboard itself: users, roles and notifications answer there about the platform's own - platform
 * users, platform roles, notifications belonging to no tenant - and the UI showcase and the tenancy
 * screens read no tenant data. Any feature added later stays tenant-only until it is listed here.
 */
const platformAccessiblePathPrefixes = ['/admin/users', '/admin/roles', '/admin/notifications', '/admin/ui', platformScopedPathPrefix]

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
 * Returns true when the caller is a platform administrator acting in no tenant,
 * whose requests the API answers in platform scope.
 */
export const isPlatformAdministratorWithoutTenant = (user: GetUserInfoResponse | undefined): boolean =>
  !!user?.isPlatformAdministrator && !user.activeTenant

/**
 * Returns true when the caller may open the screen at this path: always, unless
 * they are a platform administrator acting in no tenant and the screen needs one.
 * Navigation and search use it to leave out what would only redirect.
 */
export const isPathAvailable = (user: GetUserInfoResponse | undefined, pathname: string): boolean =>
  !isPlatformAdministratorWithoutTenant(user) || !isTenantScopedPath(pathname) || isPlatformAccessiblePath(pathname)

/**
 * Decides where a platform administrator acting in no tenant goes after signing
 * in: the dashboard, rather than the no-tenant screen or the chooser, because
 * they need no tenant. A `redirect` they were sent back with is honoured when
 * they can use that screen. Returns `null` for anyone else, whose landing
 * `resolveTenantLanding` decides.
 */
export const resolvePlatformAdministratorLanding = (
  user: GetUserInfoResponse | undefined,
  redirectTo?: string | null,
): string | null => {
  if (!isPlatformAdministratorWithoutTenant(user)) return null
  if (redirectTo && isPathAvailable(user, redirectTo)) return redirectTo
  return '/admin'
}

/**
 * One entry per code the caller can be sent to the chooser with, keyed by the
 * code the API answered with: a suspension, a membership that ended and a tenant
 * that no longer exists each read differently.
 */
const tenantRefusalReasonKeys: Record<string, string> = {
  tenantSuspended: 'page.selectTenant.suspendedReason',
  tenantMembershipRevoked: 'page.selectTenant.revokedReason',
  notTenantMember: 'page.selectTenant.revokedReason',
  tenantNotFound: 'page.selectTenant.unavailableReason',
  noActiveTenant: 'page.selectTenant.unavailableReason',
}

/**
 * The translation key of the message that explains, on the chooser, why the
 * caller was sent there, given the tenant error code the API refused their last
 * request with. A code with no explanation of its own - `permissionDenied`, or
 * anything a later feature adds - yields `undefined`, so the chooser shows no
 * reason rather than a raw key to the person reading it (AC-070). A refusal that
 * arrives with no code at all is the plain case of a screen that needs a tenant
 * before it can open.
 */
export const tenantRefusalReasonKey = (code: string | null | undefined): string | undefined => {
  if (!code) return undefined
  return tenantRefusalReasonKeys[code]
}
