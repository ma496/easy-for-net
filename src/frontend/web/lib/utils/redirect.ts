/** Returns whether a redirect stays within this application. */
export const isValidRedirectPath = (path: string): boolean => {
  return path.startsWith('/') && !path.startsWith('//') && path !== '/signin'
}

/**
 * The address to load once a sign-in has established a session: `path` resolved on `origin`, reduced to
 * its path, search and hash - or the dashboard when it resolves anywhere else. `isValidRedirectPath`
 * screens the text; this screens what a browser makes of it, which is what a full page load follows: a
 * browser reads `/\evil.example` as `//evil.example`, another host, and that must land on `/` instead.
 */
export const signedInLandingHref = (path: string, origin: string): string => {
  try {
    const url = new URL(path, origin)
    if (url.origin === origin) return `${url.pathname}${url.search}${url.hash}`
  } catch {
    // Not a URL at all: fall through to the dashboard.
  }
  return '/'
}

/**
 * Enters the app once a sign-in has established a session, with a full page load rather than a
 * client-side navigation. The anonymous document's router has prefetched the links on the sign-in
 * screen (the brand mark links to `/`) while there was no session, and the proxy answered each with a
 * redirect to sign-in; Next keeps that redirect as the route's cached tree for minutes, and
 * `router.refresh()` clears only the current route. A soft navigation to `/` would replay it - and land
 * back on `/signin?redirect=%2F` whenever the cached tree wins the race with the live response, as it
 * does on any real network. A new document starts with an empty router cache and a store read from
 * the session, so the server's view and the client's agree from its first request. `replace` keeps the
 * sign-in form out of the history, as `leaveSignedOut` does on the way out.
 */
export const enterSignedIn = (path: string): void => {
  window.location.replace(signedInLandingHref(path, window.location.origin))
}
