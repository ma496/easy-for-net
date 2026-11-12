/** What the locale guard needs to decide whether the URL's locale segment still agrees with the acting scope. */
export interface LocaleGuardInput {
  /** The locale segment the current URL carries (or the default locale, when it carries none). */
  urlLocale: string
  /** The culture the server actually served: the one requested, if it is enabled, else the scope's default, else `en`, else the first enabled culture. */
  servedCulture: string
  /** The acting scope's configured default culture, or `null` when it has none. */
  defaultCulture: string | null
  /** The codes the served response enables for the acting scope - `defaultCulture` is only ever a safe target when it is one of these. */
  enabledCultures: string[]
  /** The locales this deployment actually routes to - a target is only ever safe when it is one of these too. */
  routableLocales: readonly string[]
  /** Whether the visitor has an explicit `preferred-language` cookie standing - a choice this guard must never override. */
  hasPreferredLanguageCookie: boolean
}

/**
 * Decides whether the browser must be navigated to a different locale segment, first answer wins:
 *
 * - the server already resolved the culture it actually served, so a mismatch with the URL means
 *   the requested one was not usable (unknown, or disabled for this scope) - go to what was served;
 * - otherwise, a visitor with no standing preference is shown the scope's own default rather than
 *   whatever the URL happened to negotiate to, once that default is known;
 * - otherwise the URL is already right, and there is nothing to do.
 *
 * A target is only ever proposed when it is both routable and enabled for the acting scope - a
 * defensive safety net alongside the server's own contract, so a stale or malformed input can, at
 * worst, leave the guard doing nothing rather than navigate somewhere that immediately bounces back.
 *
 * Returns `null` when no navigation is needed.
 */
export const decideLocaleGuardTarget = ({ urlLocale, servedCulture, defaultCulture, enabledCultures, routableLocales, hasPreferredLanguageCookie }: LocaleGuardInput): string | null => {
  const isSafeTarget = (code: string): boolean => routableLocales.includes(code) && enabledCultures.includes(code)

  if (servedCulture !== urlLocale && isSafeTarget(servedCulture)) return servedCulture
  if (!hasPreferredLanguageCookie && defaultCulture && defaultCulture !== urlLocale && isSafeTarget(defaultCulture)) return defaultCulture
  return null
}
