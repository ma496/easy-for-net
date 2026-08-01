import type { Locale } from './config'

const dictionaries = {
  en: () => import('../public/locales/en.json').then((module) => module.default),
  ur: () => import('../public/locales/ur.json').then((module) => module.default),
  zh: () => import('../public/locales/zh.json').then((module) => module.default),
  ar: () => import('../public/locales/ar.json').then((module) => module.default),
  hi: () => import('../public/locales/hi.json').then((module) => module.default),
  es: () => import('../public/locales/es.json').then((module) => module.default),
  fr: () => import('../public/locales/fr.json').then((module) => module.default),
  ru: () => import('../public/locales/ru.json').then((module) => module.default),
}

/**
 * This method only should used in TranslationProvider and getServerTranslation.
 * When you need to use in server components, please use getServerTranslation instead of getDictionary.
 */
/** Lazily loads the translation dictionary for the given locale, falling back to English when missing. Intended for use in TranslationProvider and getServerTranslation. */
export const getDictionary = async (locale: Locale) => dictionaries[locale]?.() ?? dictionaries.en()

/**
 * This method you should use in server components.
 * Get a single localization value by key. Supports nested keys with dot notation (e.g., "brand.name").
 */
/** Server-component helper that resolves a single translation key (dot-notation) to a string for the given locale. */
export const getServerTranslation = async (langCode: string, key: string): Promise<string> => {
  const dict = await getDictionary(langCode as Locale)
  const keys = key.split('.')
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let result: any = dict
  for (const k of keys) {
    result = result?.[k]
  }
  return result ?? key
}
