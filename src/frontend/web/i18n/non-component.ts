import { globalDictionary } from "@/components/layouts";
import { i18nConfig } from "./config";

/**
 * Support for non-component files.
 * WARNING: This functions relies on the TranslationProvider being mounted to populate globalDictionary.
 * It does NOT support reactive updates for language changes (requires full page reload or manual re-call).
 */
export const getTranslation = () => {
  const dictionary = globalDictionary

  const t = (key: string, variables?: Record<string, string | number>) => {
    const keys = key.split('.');
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    let text: any = dictionary;

    if (!text) return key;

    for (const k of keys) {
      if (text && typeof text === 'object' && k in text) {
        text = text[k];
      } else {
        text = key;
        break;
      }
    }

    if (typeof text !== 'string') {
      text = key;
    }

    if (variables) {
      Object.entries(variables).forEach(([k, v]) => {
        text = text.replace(`\${${k}}`, String(v))
      })
    }
    return text
  }

  const changeLanguage = (locale: string) => {
    const pathname = window.location.pathname
    const segments = pathname.split('/')

    // handle case where pathname starts with a locale
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    if (segments.length > 1 && i18nConfig.locales.includes(segments[1] as any)) {
      segments[1] = locale
    } else {
      segments.splice(1, 0, locale)
    }

    // If setting to default locale, remove the locale segment
    if (locale === i18nConfig.defaultLocale) {
      // Find if we have a locale segment and remove it
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      if (segments.length > 1 && i18nConfig.locales.includes(segments[1] as any)) {
        segments.splice(1, 1);
      }
    }

    const newPath = segments.join('/') || '/'
    window.location.href = newPath
  }

  const i18n = {
    language: 'en', // Best effort guess or need to parse URL manually if needed
    changeLanguage,
  }

  return { t, i18n }
}
