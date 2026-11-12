import { isAuthRequired } from '@/auth-urls'

// Module state, not store state: it has to outlive nothing but the document it is set in, and the
// navigation it records discards that document - and the store with it - anyway.
let leaving = false

/**
 * Records that the app is already leaving for the sign-in page because the session has ended
 * (`leaveSignedOut`). From then on nothing else may start a navigation of its own: a request still in
 * flight - or the notification hub's reconnect probe, once sign-out has closed its connection - finds no
 * session to refresh, and its redirect would replace the one under way with `/signin?redirect=<the page
 * just left>`, sending the next person to sign in on this browser to the previous user's page.
 */
export const markLeavingSignedOut = (): void => {
  leaving = true
}

/** Whether {@link markLeavingSignedOut} has been called in this document. */
export const isLeavingSignedOut = (): boolean => leaving

/**
 * Where to send the browser once a refresh has failed: the sign-in page, returning to `pathname`
 * afterwards, when that path requires a session - or nowhere, when it does not, or when the app is
 * already leaving for sign-in.
 */
export const signinRedirectAfterFailedRefresh = (pathname: string | undefined, origin: string): string | null => {
  if (leaving || !pathname || !isAuthRequired(pathname)) return null
  const signinUrl = new URL('/signin', origin)
  signinUrl.searchParams.set('redirect', pathname)
  return signinUrl.toString()
}

/** Clears the flag - for tests, which share one module instance across cases. */
export const resetLeavingSignedOutForTests = (): void => {
  leaving = false
}
