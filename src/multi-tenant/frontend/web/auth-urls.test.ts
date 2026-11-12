import { describe, expect, it } from 'vitest'
import { Allow } from './allow'
import { i18nConfig } from './i18n/config'
import { authUrls, getMatchedAuthUrl, isAuthRequired, type AuthUrl } from './auth-urls'

describe('getMatchedAuthUrl', () => {
  it.each([
    // The list answers about every tenant there is, so Tenant.View is platform-scoped on the API: a
    // session carries it only while acting in no tenant, which is what keeps this the platform's
    // screen rather than one a tenant administrator reaches.
    ['/tenants', Allow.Tenant_View],
    ['/tenants/create', Allow.Tenant_Create],
    // Both-scoped, so this one route serves a platform administrator editing the platform's own
    // overrides and a tenant administrator editing its tenant's, gated on the same permission.
    ['/localization', Allow.Localization_View],
    // Both-scoped as well: the platform's settings in no tenant, the tenant's own inside one.
    ['/settings', Allow.Settings_View],
  ])('guards %s with the permission the screen requires', (url, permission) => {
    expect(getMatchedAuthUrl(url)?.permissions).toEqual([permission])
  })

  it.each([
    ['/tenants/6f5c1f1e-0000-0000-0000-000000000000/update', Allow.Tenant_Update],
    ['/tenants/6f5c1f1e-0000-0000-0000-000000000000/members', Allow.TenantMember_View],
    ['/tenants/6f5c1f1e-0000-0000-0000-000000000000/detail', Allow.Tenant_Detail],
  ])('guards %s with the permission the screen requires', (url, permission) => {
    expect(getMatchedAuthUrl(url)?.permissions).toEqual([permission])
  })

  it.each(['/tenants/update', '/tenants/members', '/tenants/detail'])(
    'does not match %s, which names no tenant to work with',
    (url) => {
      expect(getMatchedAuthUrl(url)).toBeUndefined()
    },
  )

  it.each(['/select-tenant'])('guards %s without requiring a permission of its own', (url) => {
    const matched = getMatchedAuthUrl(url)

    expect(matched).toBeDefined()
    expect(matched?.permissions).toBeUndefined()
  })

  it('matches a path whatever query string it is reached with', () => {
    expect(getMatchedAuthUrl('/tenants?page=2&search=acme')?.permissions).toEqual([Allow.Tenant_View])
  })

  it('matches nothing outside the registry', () => {
    expect(getMatchedAuthUrl('/tenants/list')).toBeUndefined()
    expect(getMatchedAuthUrl('/signin')).toBeUndefined()
  })

  it('refuses to answer when two entries match the same path', () => {
    const duplicate: AuthUrl = { url: '/tenants', permissions: [Allow.Tenant_Detail] }
    authUrls.push(duplicate)

    try {
      expect(() => getMatchedAuthUrl('/tenants')).toThrow(/Multiple auth URLs matched/)
    } finally {
      authUrls.splice(authUrls.indexOf(duplicate), 1)
    }
  })
})

describe('isAuthRequired', () => {
  // The app has no public pages: the dashboard is the home page, and a screen nobody registered is
  // guarded all the same.
  it.each(['/', '/tenants', '/tenants/create', '/select-tenant', '/profile', '/notifications/abc', '/some-new-feature', '/signin-help'])(
    'requires a signed-in caller for %s',
    (url) => {
      expect(isAuthRequired(url)).toBe(true)
    },
  )

  it.each([
    '/signin',
    '/signin?redirect=%2Fusers',
    '/signup',
    '/forget-password',
    '/reset-password',
    '/verify-email',
    '/ticket-signin',
    '/unauthorized',
  ])('asks nothing of the caller for %s', (url) => {
    expect(isAuthRequired(url)).toBe(false)
  })

  // A non-default locale this deployment actually routes; a project that ships only English has none.
  const otherLocale = i18nConfig.locales.find((locale) => locale !== i18nConfig.defaultLocale)

  it.skipIf(!otherLocale)('reads past the locale segment of a prefixed path', () => {
    expect(isAuthRequired(`/${otherLocale}/signin`)).toBe(false)
    expect(isAuthRequired(`/${otherLocale}/users`)).toBe(true)
  })
})
