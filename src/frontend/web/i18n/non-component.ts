import { globalDictionary } from '@/components/layouts'
import { translate } from './translate'
import { withLocale } from './locale-path'

/**
 * Support for non-component files.
 * WARNING: This functions relies on the TranslationProvider being mounted to populate globalDictionary.
 * It does NOT support reactive updates for language changes (requires full page reload or manual re-call).
 */
export const getTranslation = () => {
  const dictionary = globalDictionary

  const t = (key: string, variables?: Record<string, string | number>) => translate(dictionary.resources, key, variables)

  const changeLanguage = (locale: string) => {
    // No router outside React, and globalDictionary only refreshes on a reload, so this is a full
    // document navigation on purpose — built as an absolute URL on the current origin.
    const target = new URL(`${withLocale(window.location.pathname, locale)}${window.location.search}`, window.location.origin)
    window.location.assign(target.href)
  }

  const i18n = {
    language: dictionary.culture || 'en',
    changeLanguage,
  }

  return { t, i18n }
}
