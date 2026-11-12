import { describe, expect, it } from 'vitest'
import { Allow } from './allow'
import { i18nConfig } from './i18n/config'
import { authUrls, getMatchedAuthUrl, isAuthRequired, type AuthUrl } from './auth-urls'

describe('getMatchedAuthUrl', () => {
  it.each([
    ['/admin', [Allow.Admin_Access]],
    ['/admin/users', [Allow.Admin_Access, Allow.User_View]],
    ['/admin/localization', [Allow.Admin_Access, Allow.Localization_View]],
    ['/admin/settings', [Allow.Admin_Access, Allow.Settings_View]],
    ['/admin/seo', [Allow.Admin_Access, Allow.Seo_View]],
    ['/admin/seo/create', [Allow.Admin_Access, Allow.Seo_Update]],
    ['/admin/users/6f5c1f1e-0000-0000-0000-000000000000/update', [Allow.Admin_Access, Allow.User_Update]],
    ['/admin/seo/6f5c1f1e-0000-0000-0000-000000000000/update', [Allow.Admin_Access, Allow.Seo_Update]],
  ])('guards %s with the admin gate and the permission the screen requires', (url, permissions) => {
    expect(getMatchedAuthUrl(url)?.permissions).toEqual(permissions)
  })

  it('holds an admin screen nobody registered to the admin gate all the same', () => {
    expect(getMatchedAuthUrl('/admin/notifications')?.permissions).toEqual([Allow.Admin_Access])
    expect(getMatchedAuthUrl('/admin/some-new-screen')?.permissions).toEqual([Allow.Admin_Access])
  })

  it.each(['/account/profile', '/account/change-password'])('guards %s without requiring a permission of its own', (url) => {
    const matched = getMatchedAuthUrl(url)

    expect(matched).toBeDefined()
    expect(matched?.permissions).toBeUndefined()
  })

  it('matches a path whatever query string it is reached with', () => {
    expect(getMatchedAuthUrl('/admin/users?page=2&search=acme')?.permissions).toEqual([Allow.Admin_Access, Allow.User_View])
  })

  it('matches nothing on the public site', () => {
    expect(getMatchedAuthUrl('/')).toBeUndefined()
    expect(getMatchedAuthUrl('/about')).toBeUndefined()
    expect(getMatchedAuthUrl('/signin')).toBeUndefined()
  })

  it('refuses to answer when two entries match the same path', () => {
    const duplicate: AuthUrl = { url: '/admin/users', permissions: [Allow.User_Delete] }
    authUrls.push(duplicate)

    try {
      expect(() => getMatchedAuthUrl('/admin/users')).toThrow(/Multiple auth URLs matched/)
    } finally {
      authUrls.splice(authUrls.indexOf(duplicate), 1)
    }
  })
})

describe('isAuthRequired', () => {
  it.each(['/admin', '/admin/users', '/admin/seo/abc/update', '/admin/notifications/abc', '/account/profile', '/account/change-password'])(
    'requires a signed-in caller for %s',
    (url) => {
      expect(isAuthRequired(url)).toBe(true)
    },
  )

  it.each(['/', '/about', '/contact', '/some-new-page', '/administrators', '/signin', '/signin?redirect=%2Fadmin', '/signup', '/forget-password', '/reset-password', '/verify-email', '/unauthorized'])(
    'asks nothing of the caller for %s',
    (url) => {
      expect(isAuthRequired(url)).toBe(false)
    },
  )

  // A non-default locale this deployment actually routes; a project that ships only English has none.
  const otherLocale = i18nConfig.locales.find((locale) => locale !== i18nConfig.defaultLocale)

  it.skipIf(!otherLocale)('reads past the locale segment of a prefixed path', () => {
    expect(isAuthRequired(`/${otherLocale}/about`)).toBe(false)
    expect(isAuthRequired(`/${otherLocale}/admin/users`)).toBe(true)
  })
})
