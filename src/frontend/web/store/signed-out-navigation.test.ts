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
    const url = new URL(signinRedirectAfterFailedRefresh('/en/admin/users/list', origin)!)

    expect(url.origin).toBe(origin)
    expect(url.pathname).toBe('/signin')
    expect(url.searchParams.get('redirect')).toBe('/en/admin/users/list')
  })

  it('leaves a public page where it is', () => {
    expect(signinRedirectAfterFailedRefresh('/en', origin)).toBeNull()
    expect(signinRedirectAfterFailedRefresh(undefined, origin)).toBeNull()
  })

  it('never redirects over a navigation to sign-in already under way', () => {
    // Sign-out has ended the session and leaveSignedOut is loading the sign-in page: a request still in
    // flight (or the hub's reconnect probe) whose refresh fails must not replace that navigation with one
    // that returns the next user to the page just left.
    markLeavingSignedOut()

    expect(isLeavingSignedOut()).toBe(true)
    expect(signinRedirectAfterFailedRefresh('/en/admin/users/list', origin)).toBeNull()
  })
})
