import { describe, expect, it } from 'vitest'
import { Allow } from './allow'
import { authUrls, getMatchedAuthUrl, isAuthRequired, type AuthUrl } from './auth-urls'

describe('getMatchedAuthUrl', () => {
  it.each([
    // The list answers about every tenant there is, so Tenant.View is platform-scoped on the API: a
    // session carries it only while acting in no tenant, which is what keeps this the platform's
    // screen rather than one a tenant administrator reaches.
    ['/admin/tenants/list', Allow.Tenant_View],
    ['/admin/tenants/create', Allow.Tenant_Create],
  ])('guards %s with the permission the screen requires', (url, permission) => {
    expect(getMatchedAuthUrl(url)?.permissions).toEqual([permission])
  })

  it.each([
    ['/admin/tenants/update/6f5c1f1e-0000-0000-0000-000000000000', Allow.Tenant_Update],
    ['/admin/tenants/members/6f5c1f1e-0000-0000-0000-000000000000', Allow.TenantMember_View],
    ['/admin/tenants/detail/6f5c1f1e-0000-0000-0000-000000000000', Allow.Tenant_Detail],
  ])('guards %s with the permission the screen requires', (url, permission) => {
    expect(getMatchedAuthUrl(url)?.permissions).toEqual([permission])
  })

  it.each(['/admin/tenants/update', '/admin/tenants/members', '/admin/tenants/detail'])(
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
    expect(getMatchedAuthUrl('/admin/tenants/list?page=2&search=acme')?.permissions).toEqual([Allow.Tenant_View])
  })

  it('matches nothing outside the registry', () => {
    expect(getMatchedAuthUrl('/admin/tenants')).toBeUndefined()
    expect(getMatchedAuthUrl('/signin')).toBeUndefined()
  })

  it('refuses to answer when two entries match the same path', () => {
    const duplicate: AuthUrl = { url: '/admin/tenants/list', permissions: [Allow.Tenant_Detail] }
    authUrls.push(duplicate)

    try {
      expect(() => getMatchedAuthUrl('/admin/tenants/list')).toThrow(/Multiple auth URLs matched/)
    } finally {
      authUrls.splice(authUrls.indexOf(duplicate), 1)
    }
  })
})

describe('isAuthRequired', () => {
  it.each(['/admin/tenants/list', '/admin/tenants/create', '/select-tenant'])(
    'requires a signed-in caller for %s',
    (url) => {
      expect(isAuthRequired(url)).toBe(true)
    },
  )

  it.each(['/', '/signin', '/signup'])('asks nothing of the caller for %s', (url) => {
    expect(isAuthRequired(url)).toBe(false)
  })
})
