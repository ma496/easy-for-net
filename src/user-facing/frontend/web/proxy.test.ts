import { NextRequest } from 'next/server'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { i18nConfig } from './i18n/config'

/**
 * The proxy's auth gate. Whether the request carries the auth cookie is the only input it reads
 * (`hasAuthCookie`, which reads `next/headers` and so has no request scope here), so that is what is
 * stubbed; locale routing runs for real.
 */
const auth = vi.hoisted(() => ({ signedIn: false }))

vi.mock('@/lib/utils/authentication-and-authorization', () => ({
  hasAuthCookie: async () => auth.signedIn,
}))

const { proxy } = await import('./proxy')

const origin = 'https://www.example.com'

/** A page request, as the browser sends it. */
const request = (path: string, headers: Record<string, string> = {}) => new NextRequest(new URL(path, origin), { headers: { 'accept-language': 'en', ...headers } })

/** Where a redirecting response sends the browser, as a path and query on the request's origin. */
const redirectTarget = (response: Response) => {
  const location = response.headers.get('location')
  if (!location) return null
  const url = new URL(location, origin)
  return `${url.pathname}${url.search}`
}

beforeEach(() => {
  auth.signedIn = false
})

describe('proxy auth gate', () => {
  it.each(['/', '/about', '/contact', '/signin', '/signin?redirect=%2Fadmin', '/signup'])('lets a visitor with no session through to %s', async (path) => {
    const response = await proxy(request(path))

    expect(response.headers.get('location')).toBeNull()
  })

  it.each([
    ['/admin', '/signin?redirect=%2Fadmin'],
    ['/admin/users', '/signin?redirect=%2Fadmin%2Fusers'],
    ['/account/profile', '/signin?redirect=%2Faccount%2Fprofile'],
  ])('sends a visitor with no session from %s to sign in, returning there afterwards', async (path, target) => {
    const response = await proxy(request(path))

    expect(response.status).toBe(307)
    expect(redirectTarget(response)).toBe(target)
  })

  // A project generated with English only routes no other locale, so there is nothing to prefix.
  const otherLocale = i18nConfig.locales.find((locale) => locale !== i18nConfig.defaultLocale)

  it.skipIf(!otherLocale)('names the screen asked for as the redirect, without its locale segment', async () => {
    expect(redirectTarget(await proxy(request(`/${otherLocale}/admin/users`, { cookie: `preferred-language=${otherLocale}` })))).toBe(`/${otherLocale}/signin?redirect=%2Fadmin%2Fusers`)
  })

  it.each(['/admin', '/admin/users', '/admin/users?page=2', '/account/profile'])('lets a request carrying the session through to %s', async (path) => {
    auth.signedIn = true

    const response = await proxy(request(path))

    expect(response.headers.get('location')).toBeNull()
  })
})
