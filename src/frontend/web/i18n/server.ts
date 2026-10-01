import { cache } from 'react'
import { environment } from '@/config'
import { i18nConfig, type Locale } from './config'
import { translate } from './translate'
import { emptyLocalizationResources, type LocalizationResourcesResponse } from './types'

/**
 * Request-memoized fetch of the merged localization resources for one culture from the API,
 * forwarding the incoming `cookie` header so the caller's own tenant/platform overrides resolve
 * exactly as they would for any other request of theirs. `cache()` means every server component
 * and `getServerTranslation` call within one request shares the single fetch. Falls back to a
 * dictionary holding only the bundled offline strings (see `emptyLocalizationResources`) when the API
 * cannot be reached - every other key renders as itself rather than crashing.
 *
 * `next/headers` is loaded dynamically rather than imported at the top of the file: this module is
 * re-exported from the shared `@/i18n` barrel that client components also import (for
 * `useTranslation`), and a static `import { headers } from 'next/headers'` here would put a
 * server-only API in the module graph Next resolves for those client bundles too, which it refuses
 * to build. A dynamic `import()` inside the function body is only ever evaluated where this actually
 * runs - a Server Component - the same reasoning as the conditional `require` in
 * `lib/utils/authentication-and-authorization.ts`.
 *
 * This only should be used from the root layout and getServerTranslation.
 */
export const getDictionary = cache(async (locale: Locale): Promise<LocalizationResourcesResponse> => {
  // A locale this project does not route to (a stale param, a crawler-guessed segment) names no
  // resource file, and asking the API for it would just be the API's own unknown-culture fallback -
  // done here instead so a malformed segment can never reach the request URL unencoded either.
  const requestedLocale = i18nConfig.locales.includes(locale) ? locale : i18nConfig.defaultLocale

  try {
    const { headers } = await import('next/headers')
    const headerList = await headers()
    const cookie = headerList.get('cookie')
    const response = await fetch(`${environment.apiUrl}/localization/resources/${encodeURIComponent(requestedLocale)}`, {
      headers: { 'Accept-Language': requestedLocale, ...(cookie ? { cookie } : {}) },
      cache: 'no-store',
    })

    if (!response.ok) {
      throw new Error(`Failed to load localization resources for "${requestedLocale}": ${response.status}`)
    }

    return (await response.json()) as LocalizationResourcesResponse
  } catch (error) {
    // `headers()` itself is what tells Next this route cannot be prerendered - every static
    // generation attempt reaches here and must let that error carry on so the route is correctly
    // marked dynamic, rather than being logged as a fetch failure it never was.
    if (error && typeof error === 'object' && 'digest' in error && error.digest === 'DYNAMIC_SERVER_USAGE') {
      throw error
    }

    console.error('Failed to load localization resources', error)
    return emptyLocalizationResources(requestedLocale)
  }
})

/**
 * This method you should use in server components.
 * Resolves a single translation key from the merged resources for the given locale, with the same
 * `${name}` interpolation the client and non-component translators support.
 */
export const getServerTranslation = async (langCode: string, key: string, variables?: Record<string, string | number>): Promise<string> => {
  const dictionary = await getDictionary(langCode as Locale)
  return translate(dictionary.resources, key, variables)
}
