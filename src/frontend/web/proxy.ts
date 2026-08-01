import { type NextRequest, NextResponse } from 'next/server'
import { hasAuthCookie } from '@/lib/utils'
import { isAuthRequired } from './auth-urls'
import { i18nConfig } from './i18n'
import { match as matchLocale } from '@formatjs/intl-localematcher'
import Negotiator from 'negotiator'

/**
 * Determines the best-matching locale for an incoming request by
 * negotiating the Accept-Language header against the configured locales,
 * falling back to the default locale.
 */
function getLocale(request: NextRequest): string | undefined {
  // Negotiator expects plain object so we need to transform headers
  const negotiatorHeaders: Record<string, string> = {}
  request.headers.forEach((value, key) => (negotiatorHeaders[key] = value))

  // Negotiator requires specific headers type
  const languages = new Negotiator({ headers: negotiatorHeaders }).languages()
  // Matcher types mismatch with Negotiator output? Apparently not anymore.
  const locales: string[] = [...i18nConfig.locales]
  return matchLocale(languages, locales, i18nConfig.defaultLocale)
}

/**
 * Next.js middleware that handles two concerns: locale prefixing
 * (rewriting/redirecting to the right localized path) and authentication
 * gating (redirecting unauthenticated requests to the signin page when
 * the route is registered in auth-urls.ts).
 */
export async function proxy(request: NextRequest) {
  const pathname = request.nextUrl.pathname

  // 1. Localization Logic
  const pathnameIsMissingLocale = i18nConfig.locales.every(
    (locale) => !pathname.startsWith(`/${locale}/`) && pathname !== `/${locale}`
  )

  let response: NextResponse | undefined
  let currentLocale: string

  if (pathnameIsMissingLocale) {
    const locale = getLocale(request) || i18nConfig.defaultLocale
    currentLocale = locale

    if (locale === i18nConfig.defaultLocale) {
      if (!pathname.startsWith('/api') && !pathname.startsWith('/_next') && !pathname.startsWith('/assets') && !pathname.includes('favicon.ico')) {
        // Internal rewrite for default locale
        response = NextResponse.rewrite(new URL(`/${locale}${pathname}`, request.url))
      }
    } else {
      // Redirect to localized path
      return NextResponse.redirect(new URL(`/${locale}${pathname}`, request.url))
    }
  } else {
    // Path has locale
    const segment = pathname.split('/')[1]
    currentLocale = segment

    // Check if it's default locale in URL (e.g. /en/...) and redirect to prefix-less if needed
    if (segment === i18nConfig.defaultLocale) {
      const newPathname = pathname.replace(`/${i18nConfig.defaultLocale}`, '') || '/';
      return NextResponse.redirect(new URL(newPathname, request.url));
    }
  }

  // 2. Auth Logic
  // Normalize path by stripping locale to check against auth rules
  // If we are rewriting (response exists), the effective path is `/${locale}${pathname}` (which has locale).
  // But we want to check logic against the "logical" path (admin pages defined as /admin/...).

  // Actually isAuthRequired checks for /admin/.
  // If path is `/en/admin/...`, `pathname` (original) is what we have?
  // If we have `response` (rewrite), the *original* `request.nextUrl.pathname` is `/admin/...` (missing locale).
  // If we *don't* have `response` (path has locale), `request.nextUrl.pathname` is `/en/admin/...`.

  let pathToCheck = pathname
  if (!pathnameIsMissingLocale) {
    // Remove locale prefix for auth check
    // e.g. /en/admin/dashboard -> /admin/dashboard
    // e.g. /en -> /
    pathToCheck = pathname.replace(`/${currentLocale}`, '') || '/'
  }

  // NOTE: isAuthRequired checks `url.includes('/admin/')`.
  // `/admin/dashboard` includes `/admin/`.
  // `/en/admin/dashboard` includes `/admin/`.
  // So strict normalization might not be strictly required for *inclusion* check,
  // but `getMatchedAuthUrl` does strict matching on `url`.
  // So we SHOULD normalize.

  const isAuthenticated = await hasAuthCookie()

  if (isAuthRequired(pathToCheck) && !isAuthenticated) {
    // Redirect to signin, preserving locale
    // If currentLocale is default, signin is `/signin`
    // If currentLocale is other, signin is `/${currentLocale}/signin`

    let signinPath = '/signin'
    if (currentLocale !== i18nConfig.defaultLocale) {
      signinPath = `/${currentLocale}/signin`
    }

    return NextResponse.redirect(new URL(signinPath, request.url))
  }

  return response || NextResponse.next()
}

export const config = {
  matcher: ['/((?!_next/static|_next/image|favicon.ico).*)'],
}
