import { i18nConfig } from './config'

/**
 * Swaps the locale segment of a (locale-prefixed or unprefixed) pathname for another locale,
 * adding or removing the segment as the "default locale carries no prefix" convention requires.
 * Shared by every place that navigates to a different locale: the client `changeLanguage` calls,
 * the non-component translator, and the locale guard's correction.
 */
export const withLocale = (pathname: string, locale: string): string => {
  // Normalized so the root path and a trailing slash never leave a stray empty segment behind:
  // '/' becomes '', so it is treated exactly like an unprefixed path with nothing after the locale.
  const withoutTrailingSlash = pathname !== '/' && pathname.endsWith('/') ? pathname.slice(0, -1) : pathname
  const path = withoutTrailingSlash === '/' ? '' : withoutTrailingSlash

  const segments = path.split('/')
  const hasLocaleSegment = segments.length > 1 && (i18nConfig.locales as readonly string[]).includes(segments[1])

  if (hasLocaleSegment) {
    segments[1] = locale
  } else {
    segments.splice(1, 0, locale)
  }

  if (locale === i18nConfig.defaultLocale && segments.length > 1 && segments[1] === locale) {
    segments.splice(1, 1)
  }

  // Collapsed so a pathname with an empty first segment can never come back protocol-relative ('//host'),
  // which a redirect built with new URL(path, base) would resolve to another origin.
  return segments.join('/').replace(/^\/{2,}/, '/') || '/'
}
