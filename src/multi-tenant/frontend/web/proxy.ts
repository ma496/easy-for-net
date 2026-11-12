import { type NextRequest, NextResponse } from 'next/server'
import { hasAuthCookie } from '@/lib/utils/authentication-and-authorization'
import { isAuthRequired } from './auth-urls'
import { decideLocaleRouting, i18nConfig, PREFERRED_LANGUAGE_COOKIE, SCOPE_LANGUAGE_COOKIE, SCOPE_LANGUAGES_COOKIE } from './i18n'
import { match as matchLocale } from '@formatjs/intl-localematcher'
import Negotiator from 'negotiator'

/**
 * Negotiates the best-matching locale for an incoming request from its `Accept-Language` header
 * against the configured locales, falling back to the default locale.
 */
function negotiateLocale(request: NextRequest): string | undefined {
  // Negotiator expects plain object so we need to transform headers
  const negotiatorHeaders: Record<string, string> = {}
  request.headers.forEach((value, key) => (negotiatorHeaders[key] = value))

  // A request without Accept-Language (crawlers, health probes, curl) negotiates to the wildcard `*`,
  // which is no language tag and makes matchLocale throw - so it and anything malformed fall through
  // to the default instead of failing the request.
  const languages = new Negotiator({ headers: negotiatorHeaders }).languages().filter((language: string) => language !== '*')
  const locales: string[] = [...i18nConfig.locales]
  try {
    return matchLocale(languages, locales, i18nConfig.defaultLocale)
  } catch {
    return i18nConfig.defaultLocale
  }
}

/**
 * Next.js middleware that handles two concerns: locale prefixing
 * (rewriting/redirecting to the right localized path) and authentication
 * gating (redirecting unauthenticated requests to the signin page for
 * every page but the anonymous ones auth-urls.ts names).
 */
export async function proxy(request: NextRequest) {
  const pathname = request.nextUrl.pathname
  // Framework and API requests that reach this function are neither localized nor gated - only pages are.
  const isPagePath = !pathname.startsWith('/api') && !pathname.startsWith('/_next') && !pathname.startsWith('/assets') && !pathname.includes('favicon.ico')

  // 1. Localization Logic
  const pathnameIsMissingLocale = i18nConfig.locales.every((locale) => !pathname.startsWith(`/${locale}/`) && pathname !== `/${locale}`)

  // `decideLocaleRouting` is the pure decision - the same function a test replays many hops of - so
  // everything here is just turning it into the NextResponse it calls for.
  const routing = decideLocaleRouting(
    pathname,
    i18nConfig.locales,
    i18nConfig.defaultLocale,
    {
      preferredLanguage: request.cookies.get(PREFERRED_LANGUAGE_COOKIE)?.value,
      scopeLanguage: request.cookies.get(SCOPE_LANGUAGE_COOKIE)?.value,
      scopeLanguages: request.cookies.get(SCOPE_LANGUAGES_COOKIE)?.value,
    },
    negotiateLocale(request),
  )

  let response: NextResponse | undefined
  const currentLocale = routing.locale

  if (routing.kind === 'rewrite') {
    if (isPagePath) {
      // Internal rewrite for default locale
      response = NextResponse.rewrite(new URL(routing.path, request.url))
    }
  } else if (routing.kind === 'redirect') {
    return NextResponse.redirect(new URL(routing.path, request.url))
  }

  // 2. Auth Logic
  // The auth rules are written against the logical, locale-free path, which is also what the sign-in
  // page is sent back to: a rewritten default-locale request already has no locale segment
  // (`/users`), a prefixed one has it removed (`/fr/users` -> `/users`, `/fr` -> `/`).
  let pathToCheck = pathname
  if (!pathnameIsMissingLocale) {
    pathToCheck = pathname.replace(`/${currentLocale}`, '') || '/'
  }

  const isAuthenticated = await hasAuthCookie()

  if (isPagePath && isAuthRequired(pathToCheck) && !isAuthenticated) {
    let signinPath = '/signin'
    if (currentLocale !== i18nConfig.defaultLocale) {
      signinPath = `/${currentLocale}/signin`
    }
    const signinUrl = new URL(signinPath, request.url)
    signinUrl.searchParams.set('redirect', pathToCheck)
    return NextResponse.redirect(signinUrl)
  }

  return response || NextResponse.next()
}

// Static files - `public/` and the app-root metadata files (`icon.png`, `favicon.ico`, ...) - are
// served from the site root with no locale segment, so they must bypass locale routing; otherwise
// `/icon.png` is rewritten or redirected to `/<locale>/icon.png`, which does not exist.
export const config = {
  matcher: ['/((?!_next/static|_next/image|assets/|.*\\.(?:ico|png|jpg|jpeg|gif|svg|webp|avif|txt|xml|webmanifest)$).*)'],
}
