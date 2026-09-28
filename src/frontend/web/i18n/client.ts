'use client'

import { TranslationContext } from '@/components/layouts'
import { usePathname, useRouter } from 'next/navigation'
import { useContext } from 'react'
import { translate } from './translate'
import { localeFromPathname, withLocale } from './locale-path'

/**
 * Client-side React hook that reads the merged localization resources from TranslationContext and
 * exposes a `t(key, vars)` helper plus an `i18n` object with the current URL language and a
 * `changeLanguage` handler that navigates to the same path under a different locale segment.
 */
export const useTranslation = () => {
  const dictionary = useContext(TranslationContext)
  const router = useRouter()
  const pathname = usePathname()

  const t = (key: string, variables?: Record<string, string | number>) => translate(dictionary.resources, key, variables)

  const changeLanguage = (locale: string) => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    router.push(`${withLocale(pathname, locale)}${window.location.search}` as any)
  }

  const language = localeFromPathname(pathname)

  const i18n = {
    language,
    changeLanguage,
  }

  return { t, i18n }
}
