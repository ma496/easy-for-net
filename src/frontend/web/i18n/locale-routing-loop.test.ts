import { describe, expect, it } from 'vitest'
import { i18nConfig } from './config'
import { decideLocaleGuardTarget } from './locale-guard'
import { decideLocaleRouting, type LocaleRoutingCookies } from './locale-routing'
import { withLocale } from './locale-path'

/**
 * This is the invariant the two redirect-loop bugs broke: a stale `preferred-language` cookie naming
 * a culture the acting scope has since disabled, and a URL locale segment the scope no longer enables,
 * used to bounce a visitor back and forth forever between what the URL asked for and what the API
 * actually served. `decideLocaleRouting` and `decideLocaleGuardTarget` are kept pure precisely so the
 * whole chain - proxy decision, the API's own served-culture rule, the client guard's cookie and
 * navigation decision, the proxy again - can be replayed here without a `NextRequest` in sight.
 *
 * `withLocale` (used both inside `decideLocaleRouting` and below, for the guard's own navigation) reads
 * the *real* `i18nConfig`, not a parameter, so the matrix has to be built from two of this deployment's
 * actual non-default locales rather than made-up codes - which is also why the whole suite is skipped
 * for a `-m false` (English-only) project: with nothing to redirect to or from, the scenario this test
 * exists for cannot arise.
 */

const defaultLocale = i18nConfig.defaultLocale
const [localeA, localeB] = i18nConfig.locales.filter((locale) => locale !== defaultLocale)
const routableLocales = [defaultLocale, localeA, localeB].filter(Boolean) as string[]
const hasEnoughLocales = Boolean(localeA && localeB)

/** One acting scope's language configuration, as `GET /localization/resources/{culture}` would report it. */
interface LanguageSetting {
  enabledCultures: string[]
  defaultCulture: string | null
}

/**
 * The API's own served-culture rule (contract §Endpoints/1): the culture requested, if enabled, else
 * the scope's default, else `en`, else whichever culture is enabled at all. `EnabledCultures` is a
 * non-empty invariant of `LanguageSetting`, so the last branch always has something to return.
 */
const serveCulture = (requested: string, { enabledCultures, defaultCulture }: LanguageSetting): string => {
  if (enabledCultures.includes(requested)) return requested
  if (defaultCulture && enabledCultures.includes(defaultCulture)) return defaultCulture
  if (enabledCultures.includes(defaultLocale)) return defaultLocale
  return enabledCultures[0]
}

/** One playback of the whole chain, starting from one browser state, until it settles or a bound is exceeded. */
const settle = (startPath: string, startCookies: LocaleRoutingCookies, negotiatedLocale: string | undefined, languageSetting: LanguageSetting) => {
  let pathname = startPath
  let cookies = startCookies
  const requestedPaths = [pathname]
  let hops = 0

  // A generous bound well above the "within 2 hops" requirement - large enough that a genuine loop is
  // unmistakable, small enough that a broken invariant fails fast instead of hanging the test run.
  for (let iteration = 0; iteration < 10; iteration++) {
    const routing = decideLocaleRouting(pathname, routableLocales, defaultLocale, cookies, negotiatedLocale)

    if (routing.kind === 'redirect') {
      hops++
      pathname = routing.path
      requestedPaths.push(pathname)
      continue
    }

    // 'rewrite' (no locale segment, resolves to the default) or 'none' (segment already canonical):
    // either way the page is now served under `routing.locale`, so the API and the guard run next.
    const served = serveCulture(routing.locale, languageSetting)
    const enabledCultures = languageSetting.enabledCultures
    const preferredIsStale = enabledCultures.length > 0 && cookies.preferredLanguage != null && !enabledCultures.includes(cookies.preferredLanguage)

    const nextCookies: LocaleRoutingCookies = {
      preferredLanguage: preferredIsStale ? undefined : cookies.preferredLanguage,
      scopeLanguage: languageSetting.defaultCulture ?? undefined,
      scopeLanguages: enabledCultures.length > 0 ? enabledCultures.join(',') : undefined,
    }

    const target = decideLocaleGuardTarget({
      urlLocale: routing.locale,
      servedCulture: served,
      defaultCulture: languageSetting.defaultCulture,
      enabledCultures,
      routableLocales,
      hasPreferredLanguageCookie: nextCookies.preferredLanguage != null,
    })

    cookies = nextCookies

    if (!target) {
      return { settledPath: pathname, hops, requestedPaths }
    }

    hops++
    pathname = withLocale(pathname, target)
    requestedPaths.push(pathname)
  }

  throw new Error(`did not settle within 10 iterations from "${startPath}" - requested ${requestedPaths.join(' -> ')}`)
}

describe.skipIf(!hasEnoughLocales)('the proxy/guard locale negotiation loop', () => {
  /** Every scope configuration the matrix exercises, including one that disables the default locale outright. */
  const languageSettings: [label: string, setting: LanguageSetting][] = [
    ['no configured default, every locale enabled', { enabledCultures: routableLocales, defaultCulture: null }],
    [`${localeA} as the scope default`, { enabledCultures: routableLocales, defaultCulture: localeA }],
    [`${defaultLocale} disabled outright, ${localeB} the default`, { enabledCultures: [localeA, localeB], defaultCulture: localeB }],
    ['only one culture enabled', { enabledCultures: [localeB], defaultCulture: null }],
  ]

  /** Every `preferred-language` cookie state the matrix exercises. */
  const preferredCookies: [label: string, value: string | undefined][] = [
    ['absent', undefined],
    [`naming an enabled culture (${localeB})`, localeB],
    ['naming a culture no scope in this matrix enables', 'zz'],
  ]

  /** Every `Accept-Language` negotiation outcome the matrix exercises. */
  const negotiations: [label: string, value: string | undefined][] = [
    ['none (no header, or nothing matched)', undefined],
    [`hits a routable culture (${localeA})`, localeA],
  ]

  /** Every starting URL shape the matrix exercises: prefixless, and prefixed with each routable locale. */
  const startPaths: string[] = ['/admin/users', `/${defaultLocale}/admin/users`, `/${localeA}/admin/users`, `/${localeB}/admin/users`]

  const cases = languageSettings.flatMap(([settingLabel, setting]) =>
    preferredCookies.flatMap(([preferredLabel, preferred]) =>
      negotiations.flatMap(([negotiationLabel, negotiated]) =>
        startPaths.map((startPath): [string, LanguageSetting, string | undefined, string | undefined, string] => [
          `${startPath} | scope: ${settingLabel} | preferred: ${preferredLabel} | Accept-Language: ${negotiationLabel}`,
          setting,
          preferred,
          negotiated,
          startPath,
        ]),
      ),
    ),
  )

  it(`covers a real matrix of scopes, cookies and starting URLs (${cases.length} cases)`, () => {
    expect(cases.length).toBeGreaterThanOrEqual(24)
  })

  it.each(cases)('%s settles within 2 hops and never revisits a URL', (_label, setting, preferred, negotiated, startPath) => {
    const startCookies: LocaleRoutingCookies = { preferredLanguage: preferred, scopeLanguage: undefined, scopeLanguages: undefined }

    const { hops, requestedPaths } = settle(startPath, startCookies, negotiated, setting)

    expect(hops, `took ${hops} hops: ${requestedPaths.join(' -> ')}`).toBeLessThanOrEqual(2)
    expect(new Set(requestedPaths).size, `revisited a URL: ${requestedPaths.join(' -> ')}`).toBe(requestedPaths.length)
  })

  it('settles a stale preferred-language cookie together with a stale scope-languages cookie in one pass', () => {
    // The exact shape of the original bug: the visitor chose localeA while it was enabled, the scope
    // has since disabled it, and the URL still carries it too - two stale signals agreeing with each
    // other and disagreeing with the server.
    const setting: LanguageSetting = { enabledCultures: [defaultLocale, localeB], defaultCulture: localeB }
    const startCookies: LocaleRoutingCookies = {
      preferredLanguage: localeA,
      scopeLanguage: localeA,
      scopeLanguages: routableLocales.join(','),
    }

    const { hops, settledPath } = settle(`/${localeA}/admin/users`, startCookies, undefined, setting)

    expect(hops).toBeLessThanOrEqual(2)
    expect(settledPath).toBe(`/${localeB}/admin/users`)
  })
})
