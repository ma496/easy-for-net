import { cache } from 'react'
import { MissingServerApiUrlError } from '@/config'
import { serverApiFetch, type ServerApiFetchInit } from '@/lib/server/api-fetch'
import { i18nConfig, type Locale } from './config'
import { translate } from './translate'
import { emptyLocalizationResources, type LocalizationResourcesResponse } from './types'

/** The cookies that mean the visitor holds a session (the API's auth cookie, or its refresh token). */
const SESSION_COOKIES = ['.AspNetCore.Cookies', 'refreshToken']

/** The cache tag every cached dictionary fetch carries, which the admin screens revalidate after an edit. */
export const LOCALIZATION_CACHE_TAG = 'localization'

/**
 * How the dictionary is fetched. A visitor with no session gets the same answer as every other anonymous
 * visitor, so the fetch is cached for five minutes under the `localization` tag (revalidated at once when
 * an administrator saves a text or a language setting) and sends no cookies. A signed-in visitor's fetch
 * forwards their cookies and is never cached, so an administrator sees their own edit on the next request.
 */
export const dictionaryFetchInit = (isSignedIn: boolean): Pick<ServerApiFetchInit, 'forwardCookies' | 'cache' | 'next'> =>
  isSignedIn ? { forwardCookies: true, cache: 'no-store' } : { forwardCookies: false, next: { revalidate: 300, tags: [LOCALIZATION_CACHE_TAG] } }

/** Whether the incoming request carries a session cookie; read lazily, for the reason the comment on {@link getDictionary} gives. */
const requestHasSession = async (): Promise<boolean> => {
  const { cookies } = await import('next/headers')
  const jar = await cookies()
  return SESSION_COOKIES.some((name) => jar.has(name))
}

/**
 * Request-memoized fetch of the merged localization resources for one culture from the API (see
 * {@link dictionaryFetchInit} for when it is cached). `cache()` means every server component and
 * `getServerTranslation` call within one request shares the single fetch. Falls back to an empty
 * dictionary (see `emptyLocalizationResources`) when the API cannot be reached, so every call site's
 * own English default renders rather than crashing.
 *
 * `next/headers` is loaded dynamically (here and inside `serverApiFetch`) rather than imported at the top
 * of a file: this module is re-exported from the shared `@/i18n` barrel that client components also
 * import (for `useTranslation`), and a static `import { headers } from 'next/headers'` here would put a
 * server-only API in the module graph Next resolves for those client bundles too, which it refuses to
 * build. A dynamic `import()` inside the function body is only ever evaluated where this actually runs.
 *
 * This only should be used from the root layout and getServerTranslation.
 */
export const getDictionary = cache(async (locale: Locale): Promise<LocalizationResourcesResponse> => {
  // A locale this project does not route to (a stale param, a crawler-guessed segment) names no
  // resource file, and asking the API for it would just be the API's own unknown-culture fallback -
  // done here instead so a malformed segment can never reach the request URL unencoded either.
  const requestedLocale = i18nConfig.locales.includes(locale) ? locale : i18nConfig.defaultLocale

  try {
    const response = await serverApiFetch(`/localization/resources/${encodeURIComponent(requestedLocale)}`, {
      headers: { 'Accept-Language': requestedLocale },
      ...dictionaryFetchInit(await requestHasSession()),
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

    // A deployment that never told the server where the API is has nothing to fall back from - fail loudly.
    if (error instanceof MissingServerApiUrlError) {
      throw error
    }

    console.error('Failed to load localization resources', error)
    return emptyLocalizationResources(requestedLocale)
  }
})

/**
 * This method you should use in server components.
 * Resolves a single translation key from the merged resources for the given locale, with the same
 * `${name}` interpolation the client and non-component translators support, or `''` when the key is
 * missing - follow it with the English default: `(await getServerTranslation(lang, 'key')) || 'English'`.
 */
export const getServerTranslation = async (langCode: string, key: string, variables?: Record<string, string | number>): Promise<string> => {
  const dictionary = await getDictionary(langCode as Locale)
  return translate(dictionary.resources, key, variables)
}
