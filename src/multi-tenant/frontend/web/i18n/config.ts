/** Static i18n configuration defining the default locale and the list of supported locales. */
export const i18nConfig = {
  defaultLocale: 'en',
  locales: ['en', 'ur', 'zh', 'ar', 'hi', 'es', 'fr', 'ru'],
} as const

/** Union of supported locale codes derived from i18nConfig.locales. */
export type Locale = (typeof i18nConfig)['locales'][number]
