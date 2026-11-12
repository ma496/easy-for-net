/**
 * The three cookie names the locale system reads and writes, shared by the proxy (server-side,
 * `request.cookies`) and the browser (the locale guard and the language dropdown, `document.cookie`).
 * Kept here once so the name spelled in the proxy's request and the name written from the browser can
 * never drift apart.
 */
export const PREFERRED_LANGUAGE_COOKIE = 'preferred-language'
export const SCOPE_LANGUAGE_COOKIE = 'scope-language'
export const SCOPE_LANGUAGES_COOKIE = 'scope-languages'

/** How long a locale cookie written from the browser stands for, in seconds. */
const LOCALE_COOKIE_MAX_AGE_SECONDS = 60 * 60 * 24 * 365

/**
 * Reads a cookie by name from `document.cookie`, or `null` when it is not set. Browser-only - there is
 * no `document` on the server, where the proxy reads `request.cookies` directly instead.
 */
export const readLocaleCookie = (name: string): string | null => {
  const match = document.cookie.match(new RegExp(`(?:^|; )${name}=([^;]*)`))
  return match ? decodeURIComponent(match[1]) : null
}

/** Writes a locale cookie for a year, readable by the proxy on the visitor's next request. Browser-only. */
export const writeLocaleCookie = (name: string, value: string): void => {
  document.cookie = `${name}=${encodeURIComponent(value)}; path=/; max-age=${LOCALE_COOKIE_MAX_AGE_SECONDS}`
}

/** Clears a previously written locale cookie. Browser-only. */
export const clearLocaleCookie = (name: string): void => {
  document.cookie = `${name}=; path=/; max-age=0`
}
