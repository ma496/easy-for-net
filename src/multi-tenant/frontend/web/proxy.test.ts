import { NextRequest } from 'next/server'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { i18nConfig } from './i18n/config'

/**
 * The proxy's auth gate, on the requests a sign-in passes through. Whether the request carries the auth
 * cookie is the only input it reads (`hasAuthCookie`, which reads `next/headers` and so has no request
 * scope here), so that is what is stubbed; locale routing runs for real.
 */
const auth = vi.hoisted(() => ({ signedIn: false }))

vi.mock('@/lib/utils/authentication-and-authorization', () => ({
  hasAuthCookie: async () => auth.signedIn,
}))

const { proxy } = await import('./proxy')

/** A page request on a tenant's host, as the browser sends it. */
const request = (path: string, headers: Record<string, string> = {}) => new NextRequest(new URL(path, 'https://acme.example.com'), { headers: { 'accept-language': 'en', ...headers } })

/** Where a redirecting response sends the browser, as a path and query on the request's origin. */
const redirectTarget = (response: Response) => {
  const location = response.headers.get('location')
  if (!location) return null
  const url = new URL(location, 'https://acme.example.com')
  return `${url.pathname}${url.search}`
}

beforeEach(() => {
  auth.signedIn = false
})

describe('proxy auth gate', () => {
  it('sends a visitor with no session from the dashboard to sign in, returning there afterwards', async () => {
    const response = await proxy(request('/'))

    expect(response.status).toBe(307)
    expect(redirectTarget(response)).toBe('/signin?redirect=%2F')
  })

  // A project generated with English only routes no other locale, so there is nothing to prefix.
  const otherLocale = i18nConfig.locales.find((locale) => locale !== i18nConfig.defaultLocale)

  it('names the screen asked for as the redirect', async () => {
    expect(redirectTarget(await proxy(request('/users')))).toBe('/signin?redirect=%2Fusers')
  })

  it.skipIf(!otherLocale)('names the screen asked for as the redirect, without its locale segment', async () => {
    expect(redirectTarget(await proxy(request(`/${otherLocale}/users`, { cookie: `preferred-language=${otherLocale}` })))).toBe(`/${otherLocale}/signin?redirect=%2Fusers`)
  })

  it('answers a router prefetch with the same redirect, which is why the client must not reuse it once signed in', async () => {
    const response = await proxy(request('/?_rsc=abc', { rsc: '1', 'next-router-prefetch': '1' }))

    expect(redirectTarget(response)).toBe('/signin?redirect=%2F')
  })

  it('lets the sign-in screen through without a session, whatever its redirect', async () => {
    const response = await proxy(request('/signin?redirect=%2F'))

    expect(response.headers.get('location')).toBeNull()
  })

  it.each(['/', '/users', '/users?page=2'])('lets a request carrying the session through to %s - a direct visit or a reload alike', async (path) => {
    auth.signedIn = true

    const response = await proxy(request(path))

    expect(response.headers.get('location')).toBeNull()
  })
})
