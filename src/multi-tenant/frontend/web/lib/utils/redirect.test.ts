import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { enterSignedIn, isValidRedirectPath, signedInLandingHref } from './redirect'

describe('isValidRedirectPath', () => {
  it('accepts local application paths', () => {
    expect(isValidRedirectPath('/users')).toBe(true)
  })

  it.each(['https://example.com', '//example.com', '/signin'])('rejects unsafe redirect %s', (path) => {
    expect(isValidRedirectPath(path)).toBe(false)
  })
})

describe('signedInLandingHref', () => {
  const origin = 'https://acme.example.com'

  it.each([
    ['/', '/'],
    ['/users', '/users'],
    ['/fr/users', '/fr/users'],
    ['/users?page=2&sort=name#top', '/users?page=2&sort=name#top'],
  ])('keeps the intended destination %s on this origin', (path, expected) => {
    expect(signedInLandingHref(path, origin)).toBe(expected)
  })

  // Each passes `isValidRedirectPath` or would reach a full page load in some form, and each resolves
  // to another host once a browser parses it.
  it.each(['/\\evil.example', '/\\/evil.example', '//evil.example', 'https://evil.example/users', '/\t/evil.example'])(
    'lands on the dashboard rather than following %j off this origin',
    (path) => {
      expect(signedInLandingHref(path, origin)).toBe('/')
    },
  )
})

describe('enterSignedIn', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  /** A window whose location records where it was sent. */
  const stubWindow = () => {
    const replace = vi.fn()
    vi.stubGlobal('window', { location: { origin: 'https://acme.example.com', replace } })
    return replace
  }

  it('loads the landing as a new document, replacing the sign-in page in the history', () => {
    const replace = stubWindow()

    enterSignedIn('/users?page=2')

    expect(replace).toHaveBeenCalledExactlyOnceWith('/users?page=2')
  })

  it('never leaves the origin', () => {
    const replace = stubWindow()

    enterSignedIn('/\\evil.example')

    expect(replace).toHaveBeenCalledExactlyOnceWith('/')
  })
})

/**
 * The screens that turn a credential into a session and then enter the app. Each must enter with a full
 * page load: its document's router prefetched the screen's links while there was no session and the
 * proxy answered each with a redirect to sign-in, which Next keeps as the route's cached tree. A
 * client-side navigation replays that redirect - the first sign-in lands back on
 * `/signin?redirect=%2F` while the session is already valid - and `router.refresh()` does not clear it,
 * since it clears only the current route. There is no browser here to sign in with, so it is read from
 * the sources.
 */
const signinScreens = ['app/[lang]/(auth)/signin/_components/signin-form.tsx', 'app/[lang]/(auth)/ticket-signin/_components/ticket-signin-view.tsx']

/** The web root, so each screen can be read by the path it is written under. */
const webDirectory = fileURLToPath(new URL('../../', import.meta.url))

describe('entering the app after signing in', () => {
  it.each(signinScreens)('%s enters with a full page load', (file) => {
    const source = readFileSync(join(webDirectory, file), 'utf8')

    expect(source).toMatch(/enterSignedIn\(router\.localize\(/)
  })

  it.each(signinScreens)('%s does not navigate client-side into the session', (file) => {
    const source = readFileSync(join(webDirectory, file), 'utf8')

    expect(source).not.toMatch(/router\.(push|replace|refresh)\(/)
  })

  // The sign-in screens read the account info through the same cache entry as the root's first read.
  // Gating the root loader on that entry's `isLoading` swaps the sign-in page for the loader as soon as
  // the screen asks (its entry held only the anonymous 401), unmounting the form mid-submit and showing
  // it again before the browser leaves.
  it("keeps the root loader to the document's own first read of the account info", () => {
    const source = readFileSync(join(webDirectory, 'App.tsx'), 'utf8')

    expect(source).not.toMatch(/\{\s*isLoading:\s*isLoadingUserInfo\s*\}\s*\]\s*=\s*useLazyGetUserInfoQuery/)
    expect(source).toContain('!isUserInfoSettled ? <AppLoading />')
  })
})
