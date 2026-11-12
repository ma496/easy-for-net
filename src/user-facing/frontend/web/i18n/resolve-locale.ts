/**
 * The set of locales the acting scope currently enables, derived from the `scope-languages` cookie
 * the locale guard keeps in step with the served dictionary's `languages`. Untrusted input (absent,
 * empty, or naming nothing this deployment routes) always degrades to "nothing is known to be
 * disabled" - the full routable set - rather than to "nothing is enabled", so a missing or stale
 * cookie can never lock a visitor out of every locale.
 */
export const enabledLocales = (routableLocales: readonly string[], scopeLanguagesCookie: string | undefined): string[] => {
  if (!scopeLanguagesCookie) return [...routableLocales]

  const scoped = scopeLanguagesCookie
    .split(',')
    .map((code) => code.trim())
    .filter(Boolean)
  // Kept in the cookie's order - the order the API stores and falls back through ("first enabled") -
  // so the proxy lands a visitor on the same culture the API would serve.
  const intersected = scoped.filter((locale, index) => routableLocales.includes(locale) && scoped.indexOf(locale) === index)

  return intersected.length > 0 ? intersected : [...routableLocales]
}

/**
 * Chooses the locale to serve a request whose path carries no locale segment, first answer wins:
 * the visitor's own explicit choice, the acting scope's own default (read from the `scope-language`
 * cookie the locale guard keeps in step with it), what the browser's `Accept-Language` negotiates
 * to, then the project's configured default. Each candidate only counts when it names a locale that
 * is both routable and enabled for the acting scope (see {@link enabledLocales}) - a cookie naming a
 * culture the scope has since disabled is exactly the situation that used to bounce a visitor back
 * and forth between what the URL asked for and what the API actually served.
 */
export const resolveLocale = (
  routableLocales: readonly string[],
  defaultLocale: string,
  preferredLanguageCookie: string | undefined,
  scopeLanguageCookie: string | undefined,
  scopeLanguagesCookie: string | undefined,
  negotiatedLocale: string | undefined,
): string => {
  const enabled = enabledLocales(routableLocales, scopeLanguagesCookie)
  const isEnabled = (candidate: string | undefined): candidate is string => !!candidate && enabled.includes(candidate)

  if (isEnabled(preferredLanguageCookie)) return preferredLanguageCookie
  if (isEnabled(scopeLanguageCookie)) return scopeLanguageCookie
  if (isEnabled(negotiatedLocale)) return negotiatedLocale

  // No candidate applied - mirror the API's own served-culture rule, as though `defaultLocale` were
  // the culture requested: the scope's own default (already tried above) would have won were it
  // enabled, so what is left is the deployment default if enabled, else whichever locale is.
  if (isEnabled(defaultLocale)) return defaultLocale
  return enabled[0] ?? defaultLocale
}
