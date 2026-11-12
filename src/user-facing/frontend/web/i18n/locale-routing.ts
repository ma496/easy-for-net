import { withLocale } from './locale-path'
import { enabledLocales, resolveLocale } from './resolve-locale'

/** The three cookies the proxy's locale routing reads, named the way `document.cookie` and `request.cookies` both spell them. */
export interface LocaleRoutingCookies {
  preferredLanguage: string | undefined
  scopeLanguage: string | undefined
  scopeLanguages: string | undefined
}

/**
 * What the proxy should do with a request, decided from the URL alone:
 * - `'rewrite'`: serve `path` internally without changing the address bar - only for a path with no
 *   locale segment that resolves to the default locale, which carries no prefix by convention.
 * - `'redirect'`: send the browser to `path` instead.
 * - `'none'`: the URL already names the right, enabled locale; carry on.
 */
export interface LocaleRoutingDecision {
  kind: 'rewrite' | 'redirect' | 'none'
  locale: string
  path: string
}

/**
 * The proxy's locale-routing decision, pulled out of the Next.js request/response plumbing so it is a
 * plain function of the URL, the cookies and the negotiated `Accept-Language` locale - which is what
 * lets a test replay many hops of it (proxy decision -> served culture -> guard cookies -> proxy
 * decision again) without constructing a `NextRequest` for each one, and is what keeps the two
 * failure modes that used to loop forever - a disabled `preferred-language` choice, a URL locale the
 * scope no longer enables - reachable from a single, exhaustively testable place.
 *
 * A path with no locale segment always resolves to *some* locale (there is nothing to compare
 * against), so it is always `'rewrite'` or `'redirect'`. A path that already carries one is
 * `'redirect'` only when the segment names a locale that is not enabled for the acting scope, or is
 * the default locale still carrying its (by-convention invisible) prefix; otherwise it is `'none'`.
 */
export const decideLocaleRouting = (
  pathname: string,
  routableLocales: readonly string[],
  defaultLocale: string,
  cookies: LocaleRoutingCookies,
  negotiatedLocale: string | undefined,
): LocaleRoutingDecision => {
  const hasLocaleSegment = routableLocales.some((locale) => pathname === `/${locale}` || pathname.startsWith(`/${locale}/`))

  const resolve = () => resolveLocale(routableLocales, defaultLocale, cookies.preferredLanguage, cookies.scopeLanguage, cookies.scopeLanguages, negotiatedLocale)

  if (!hasLocaleSegment) {
    const locale = resolve()
    const path = `/${locale}${pathname === '/' ? '' : pathname}`
    return { kind: locale === defaultLocale ? 'rewrite' : 'redirect', locale, path }
  }

  const segment = pathname.split('/')[1]
  const enabled = enabledLocales(routableLocales, cookies.scopeLanguages)
  // The default locale's segment is never the canonical URL on its own - it always collapses to the
  // prefixless path below - so a visitor who typed it out explicitly gets exactly the same answer a
  // prefixless visitor would, decided in one hop rather than stripped first and re-negotiated on the
  // follow-up request: two hops that would otherwise stack with whatever the guard itself needs.
  const desiredLocale = segment !== defaultLocale && enabled.includes(segment) ? segment : resolve()
  const canonicalPath = withLocale(pathname, desiredLocale)

  if (canonicalPath !== pathname) {
    return { kind: 'redirect', locale: desiredLocale, path: canonicalPath }
  }

  return { kind: 'none', locale: segment, path: pathname }
}
