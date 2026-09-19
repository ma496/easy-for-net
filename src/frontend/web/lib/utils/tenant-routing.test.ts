import { describe, expect, it } from 'vitest'
import type { GetUserInfoResponse, GetUserInfoTenant } from '@/store/api/identity'
import {
  isActiveTenantStale,
  isPathAvailable,
  isPlatformAccessiblePath,
  isTenantScopedPath,
  resolvePlatformLanding,
  resolveTenantLanding,
  tenantRefusalReasonKey,
} from './tenant-routing'

/** A tenant the caller holds an active membership in, as the account info endpoint reports it. */
const tenant = (id: string): GetUserInfoTenant => ({ id, name: `Tenant ${id}`, identifier: `t-${id}` })

/** The account info the endpoint answers with, over the part a landing decision is made from. */
const userInfo = (overrides: Partial<GetUserInfoResponse> = {}): GetUserInfoResponse => ({
  id: 'user-1',
  username: 'someone',
  email: 'someone@example.com',
  isPlatform: false,
  tenants: [],
  roles: [],
  ...overrides,
})

describe('isTenantScopedPath', () => {
  it.each([
    '/admin',
    '/admin/users/list',
    '/admin/roles/create',
    '/admin/notifications',
    '/admin/tenant-settings',
  ])('treats %s as needing an active tenant', (pathname) => {
    expect(isTenantScopedPath(pathname)).toBe(true)
  })

  it.each([
    '/',
    '/signin',
    '/profile',
    '/change-password',
    '/unauthorized',
    '/select-tenant',
    '/no-tenant',
  ])('treats %s as reachable with no tenant at all', (pathname) => {
    expect(isTenantScopedPath(pathname)).toBe(false)
  })

  it.each(['/admin/tenants', '/admin/tenants/list', '/admin/tenants/create', '/admin/tenants/members/abc', '/admin/tenants/detail/abc'])(
    'treats the platform tenancy screen %s as reachable with no tenant at all',
    (pathname) => {
      expect(isTenantScopedPath(pathname)).toBe(false)
    },
  )

  it('does not mistake a tenant-scoped screen for a platform one on a shared prefix', () => {
    expect(isTenantScopedPath('/admin/tenants-report')).toBe(true)
  })

  it.each(['/admin/users/', '/admin/users?page=2', '/admin/users#section'])(
    'reads %s as the same screen as its plain form',
    (pathname) => {
      expect(isTenantScopedPath(pathname)).toBe(true)
    },
  )
})

describe('isActiveTenantStale', () => {
  it('is false while there is no selection to discard', () => {
    expect(isActiveTenantStale(undefined)).toBe(false)
    expect(isActiveTenantStale(userInfo({ tenants: [tenant('a'), tenant('b')] }))).toBe(false)
  })

  it('is false while the selection names a tenant the caller may still act in', () => {
    const user = userInfo({
      tenants: [tenant('a'), tenant('b')],
      activeTenantId: 'a',
      activeTenant: tenant('a'),
    })

    expect(isActiveTenantStale(user)).toBe(false)
  })

  it('reads the selection from the active tenant when no id was reported beside it', () => {
    const user = userInfo({ tenants: [tenant('a'), tenant('b')], activeTenant: tenant('a') })

    expect(isActiveTenantStale(user)).toBe(false)
  })

  it('is true when the selection names a membership that ended', () => {
    const user = userInfo({ tenants: [tenant('b')], activeTenantId: 'a', activeTenant: tenant('a') })

    expect(isActiveTenantStale(user)).toBe(true)
  })

  it('is true when the selection names the last membership the caller held', () => {
    const user = userInfo({ tenants: [], activeTenantId: 'a', activeTenant: tenant('a') })

    expect(isActiveTenantStale(user)).toBe(true)
  })

  it('is true when the named selection and the resolved tenant disagree', () => {
    const user = userInfo({
      tenants: [tenant('a'), tenant('b')],
      activeTenantId: 'a',
      activeTenant: tenant('b'),
    })

    expect(isActiveTenantStale(user)).toBe(true)
  })

  it('is false for a platform administrator inside a tenant they hold no membership in', () => {
    // The shape that would read as stale for anyone else - a selection absent from the tenants
    // listed - is the ordinary shape for a platform administrator, who enters a tenant on a
    // platform-scoped role and belongs to none.
    const platformAdmin = userInfo({
      isPlatform: true,
      tenants: [],
      activeTenantId: 'a',
      activeTenant: tenant('a'),
    })

    expect(isActiveTenantStale(platformAdmin)).toBe(false)
  })
})

describe('isPlatformAccessiblePath', () => {
  it.each(['/admin', '/admin/users/list', '/admin/roles/update/abc', '/admin/notifications/list', '/admin/ui/buttons', '/admin/tenants/list', '/admin/tenants/detail/abc'])(
    'lets a platform administrator with no tenant use %s',
    (pathname) => {
      expect(isPlatformAccessiblePath(pathname)).toBe(true)
    },
  )

  it.each(['/admin/users-report', '/admin/some-new-feature'])('keeps %s tenant-only', (pathname) => {
    expect(isPlatformAccessiblePath(pathname)).toBe(false)
  })
})

describe('isPathAvailable', () => {
  const platformAdmin = userInfo({ isPlatform: true })

  it('hides tenant-only screens from a platform administrator acting in no tenant', () => {
    expect(isPathAvailable(platformAdmin, '/admin/some-new-feature')).toBe(false)
    expect(isPathAvailable(platformAdmin, '/admin/users/list')).toBe(true)
    expect(isPathAvailable(platformAdmin, '/profile')).toBe(true)
  })

  it('hides nothing tenant-scoped from anyone acting in a tenant or without the platform tier', () => {
    const acting = userInfo({ isPlatform: true, tenants: [tenant('a')], activeTenant: tenant('a') })

    expect(isPathAvailable(acting, '/admin/some-new-feature')).toBe(true)
    expect(isPathAvailable(userInfo(), '/admin/some-new-feature')).toBe(true)
  })

  it("hides the platform's own screens from a caller acting in a tenant, whatever their tier", () => {
    const platformInside = userInfo({ isPlatform: true, tenants: [], activeTenant: tenant('a') })
    const memberInside = userInfo({ tenants: [tenant('a')], activeTenant: tenant('a') })

    // The permissions these declare are platform-scoped, so a tenant session never carries them:
    // the caller is in the other scope rather than short of a grant.
    expect(isPathAvailable(platformInside, '/admin/tenants/list')).toBe(false)
    expect(isPathAvailable(platformInside, '/admin/tenants/create')).toBe(false)
    expect(isPathAvailable(memberInside, '/admin/tenants/list')).toBe(false)

    // Reading one tenant and administering its members are exercisable in either scope, so they stay
    // open to a caller inside the tenant they are about.
    expect(isPathAvailable(memberInside, '/admin/tenants/detail/a')).toBe(true)
    expect(isPathAvailable(memberInside, '/admin/tenants/members/a')).toBe(true)
  })

  it("leaves the platform's own screens open to a caller acting in no tenant", () => {
    expect(isPathAvailable(platformAdmin, '/admin/tenants/list')).toBe(true)
    expect(isPathAvailable(userInfo(), '/admin/tenants/list')).toBe(true)
  })
})

describe('resolvePlatformLanding', () => {
  it('sends a platform administrator acting in no tenant to the dashboard', () => {
    expect(resolvePlatformLanding(userInfo({ isPlatform: true }))).toBe('/admin')
    expect(resolvePlatformLanding(userInfo({ isPlatform: true, tenants: [tenant('a'), tenant('b')] }))).toBe('/admin')
  })

  it('honours a redirect they can use and ignores one that needs a tenant', () => {
    const user = userInfo({ isPlatform: true })

    expect(resolvePlatformLanding(user, '/admin/users/list')).toBe('/admin/users/list')
    expect(resolvePlatformLanding(user, '/admin/some-new-feature')).toBe('/admin')
  })

  it('leaves everyone else to the tenant landing decision', () => {
    expect(resolvePlatformLanding(undefined)).toBeNull()
    expect(resolvePlatformLanding(userInfo())).toBeNull()
    expect(
      resolvePlatformLanding(userInfo({ isPlatform: true, tenants: [tenant('a')], activeTenant: tenant('a') })),
    ).toBeNull()
  })
})

describe('resolveTenantLanding', () => {
  it('leaves a caller the server knows nothing about alone', () => {
    expect(resolveTenantLanding(undefined)).toBeNull()
  })

  it('sends a caller who belongs to no tenant to the screen that explains it', () => {
    expect(resolveTenantLanding(userInfo({ tenants: [] }))).toBe('/no-tenant')
  })

  it('sends a caller with tenants to choose among but none chosen to the chooser', () => {
    const user = userInfo({ tenants: [tenant('a'), tenant('b')] })

    expect(resolveTenantLanding(user)).toBe('/select-tenant')
  })

  it('sends a caller whose selection has gone stale to choose again rather than on their way', () => {
    const suspended = userInfo({
      tenants: [tenant('b')],
      activeTenantId: 'a',
      activeTenant: tenant('a'),
    })
    const revoked = userInfo({ tenants: [], activeTenantId: 'a', activeTenant: tenant('a') })
    const deleted = userInfo({ tenants: [tenant('b')], activeTenant: tenant('a') })

    expect(resolveTenantLanding(suspended)).toBe('/select-tenant')
    expect(resolveTenantLanding(deleted)).toBe('/select-tenant')
    expect(resolveTenantLanding(revoked)).toBe('/no-tenant')
  })

  it('lets a caller whose selection still stands go wherever they were headed', () => {
    const user = userInfo({
      tenants: [tenant('a'), tenant('b')],
      activeTenantId: 'a',
      activeTenant: tenant('a'),
    })

    expect(resolveTenantLanding(user)).toBeNull()
  })

  it('lands a platform administrator nowhere, with a tenant entered or without one', () => {
    // They hold no membership, so the tenants listed for them are empty either way: the no-tenant
    // screen would tell them something true and beside the point, and the chooser would be empty.
    const outside = userInfo({ isPlatform: true, tenants: [] })
    const inside = userInfo({
      isPlatform: true,
      tenants: [],
      activeTenantId: 'a',
      activeTenant: tenant('a'),
    })

    expect(resolveTenantLanding(outside)).toBeNull()
    expect(resolveTenantLanding(inside)).toBeNull()
  })

  it('still sends an ordinary caller in the same shape to the chooser', () => {
    const member = userInfo({ tenants: [tenant('b')], activeTenantId: 'a', activeTenant: tenant('a') })

    expect(resolveTenantLanding(member)).toBe('/select-tenant')
  })
})

/**
 * The reason a caller was sent to the chooser travels as the error code the API refused their last
 * request with, so what the chooser explains is decided here rather than in the screen (AC-070).
 */
describe('tenantRefusalReasonKey', () => {
  it('explains a suspension in its own words, so a tenant being down reads differently from a choice not made', () => {
    expect(tenantRefusalReasonKey('tenantSuspended')).toBe('page.selectTenant.suspendedReason')
  })

  it.each(['tenantMembershipRevoked', 'notTenantMember'])(
    'explains %s as the membership having ended, however the API reported it',
    (code) => {
      expect(tenantRefusalReasonKey(code)).toBe('page.selectTenant.revokedReason')
    }
  )

  it.each(['tenantNotFound', 'noActiveTenant'])('explains %s as no usable tenant standing', (code) => {
    expect(tenantRefusalReasonKey(code)).toBe('page.selectTenant.unavailableReason')
  })

  it.each(['permissionDenied', 'authenticationRequired', 'somethingAddedLater'])(
    'shows no reason for %s rather than a raw key, because it is not a tenant going away',
    (code) => {
      expect(tenantRefusalReasonKey(code)).toBeUndefined()
    }
  )

  it.each([undefined, null, ''])('shows no reason when the caller arrived with none, as on a first gate', (code) => {
    expect(tenantRefusalReasonKey(code)).toBeUndefined()
  })
})
