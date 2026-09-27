// `./types` comes first: `client` reaches `components/layouts`, whose translation provider imports this barrel
// back and calls `emptyLocalizationResources` while the module loads, so it must already be evaluated.
export { emptyLocalizationResources, type LanguageDto, type LocalizationResourcesResponse } from './types'
export {
  PREFERRED_LANGUAGE_COOKIE,
  SCOPE_LANGUAGE_COOKIE,
  SCOPE_LANGUAGES_COOKIE,
  readLocaleCookie,
  writeLocaleCookie,
  clearLocaleCookie,
} from './locale-cookies'
export { i18nConfig, type Locale } from './config'
export { withLocale } from './locale-path'
export { decideLocaleGuardTarget, type LocaleGuardInput } from './locale-guard'
export { enabledLocales, resolveLocale } from './resolve-locale'
export { decideLocaleRouting, type LocaleRoutingCookies, type LocaleRoutingDecision } from './locale-routing'
export { useTranslation } from './client'
export { getDictionary, getServerTranslation } from './server'
export { getTranslation } from './non-component'
