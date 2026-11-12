import { afterEach, describe, expect, it } from 'vitest'
import {
  isLeavingSignedOut,
  markLeavingSignedOut,
  resetLeavingSignedOutForTests,
  signinRedirectAfterFailedRefresh,
} from './signed-out-navigation'

const origin = 'https://app.example.com'

describe('signinRedirectAfterFailedRefresh', () => {
  afterEach(() => resetLeavingSignedOutForTests())

  it('sends a guarded page to sign-in, returning to it afterwards', () => {
    const url = new URL(signinRedirectAfterFailedRefresh('/en/admin/users', origin)!)

    expect(url.origin).toBe(origin)
    expect(url.pathname).toBe('/signin')
    expect(url.searchParams.get('redirect')).toBe('/en/admin/users')
  })

  it('leaves an anonymous page where it is', () => {
    expect(signinRedirectAfterFailedRefresh('/en/signin', origin)).toBeNull()
    expect(signinRedirectAfterFailedRefresh('/reset-password', origin)).toBeNull()
    expect(signinRedirectAfterFailedRefresh(undefined, origin)).toBeNull()
  })

  it('never redirects over a navigation to sign-in already under way', () => {
    // Sign-out has ended the session and leaveSignedOut is loading the sign-in page: a request still in
    // flight (or the hub's reconnect probe) whose refresh fails must not replace that navigation with one
    // that returns the next user to the page just left.
    markLeavingSignedOut()

    expect(isLeavingSignedOut()).toBe(true)
    expect(signinRedirectAfterFailedRefresh('/en/admin/users', origin)).toBeNull()
  })
})

describe('signinRedirectAfterFailedRefresh on the public site', () => {
  it('leaves a public page where it is: an anonymous visitor reading it is not sent to sign in', () => {
    expect(signinRedirectAfterFailedRefresh('/', 'https://www.example.com')).toBeNull()
    expect(signinRedirectAfterFailedRefresh('/fr/about', 'https://www.example.com')).toBeNull()
  })
})
