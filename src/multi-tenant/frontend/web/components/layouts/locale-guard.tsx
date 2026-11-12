'use client'

import { useEffect } from 'react'
import { usePathname, useRouter } from 'next/navigation'
import {
  decideLocaleGuardTarget,
  i18nConfig,
  withLocale,
  type LanguageDto,
  PREFERRED_LANGUAGE_COOKIE,
  SCOPE_LANGUAGE_COOKIE,
  SCOPE_LANGUAGES_COOKIE,
  readLocaleCookie,
  writeLocaleCookie,
  clearLocaleCookie,
} from '@/i18n'

/** Props for the LocaleGuard, naming what the URL and the server already agreed on for this request. */
interface LocaleGuardProps {
  urlLocale: string
  servedCulture: string
  defaultCulture: string | null
  languages: LanguageDto[]
}

/**
 * Keeps the URL's locale segment in step with what the acting scope actually resolves to, and keeps
 * two cookies the proxy reads for the next request in step with the served dictionary: `scope-language`
 * (the scope's own default) and `scope-languages` (the codes it currently enables, comma-separated -
 * what lets the proxy notice a URL naming a locale the scope has since disabled, rather than serving it
 * and letting this guard bounce the visitor back and forth every render). A `preferred-language` cookie
 * that now names a disabled culture is cleared here too, since a stale explicit choice was the other
 * half of that loop - once cleared it no longer blocks the scope's own default from taking over below.
 * Mounted once, inside the translation provider, so it sees every change of locale, tenant or platform
 * default the root layout re-fetches for.
 */
export const LocaleGuard = ({ urlLocale, servedCulture, defaultCulture, languages }: LocaleGuardProps) => {
  const router = useRouter()
  const pathname = usePathname()

  useEffect(() => {
    const enabledCodes = languages.map((language) => language.code)
    const hasKnownEnabledSet = enabledCodes.length > 0

    if (defaultCulture) {
      writeLocaleCookie(SCOPE_LANGUAGE_COOKIE, defaultCulture)
    } else {
      clearLocaleCookie(SCOPE_LANGUAGE_COOKIE)
    }

    if (hasKnownEnabledSet) {
      writeLocaleCookie(SCOPE_LANGUAGES_COOKIE, enabledCodes.join(','))
    } else {
      // Only known when the API is unreachable - not "nothing is enabled". Clearing rather than
      // writing an empty value is what the proxy reads as "no restriction known" for the next request.
      clearLocaleCookie(SCOPE_LANGUAGES_COOKIE)
    }

    const preferredLanguage = readLocaleCookie(PREFERRED_LANGUAGE_COOKIE)
    const preferredIsStale = hasKnownEnabledSet && preferredLanguage !== null && !enabledCodes.includes(preferredLanguage)
    if (preferredIsStale) {
      clearLocaleCookie(PREFERRED_LANGUAGE_COOKIE)
    }

    const target = decideLocaleGuardTarget({
      urlLocale,
      servedCulture,
      defaultCulture,
      enabledCultures: enabledCodes,
      routableLocales: i18nConfig.locales,
      hasPreferredLanguageCookie: preferredLanguage !== null && !preferredIsStale,
    })

    if (target) {
      const nextPath = withLocale(pathname, target)
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      router.replace(`${nextPath}${window.location.search}` as any)
    }
    // Only the values the decision actually depends on: re-running on every keystroke of an
    // unrelated navigation would fight the router for no reason.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [urlLocale, servedCulture, defaultCulture, languages, pathname])

  return null
}
