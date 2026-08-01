'use client'

import { useRouter as useNextRouter } from 'next/navigation'
import { useTranslation, i18nConfig } from '@/i18n'

/**
 * Wraps Next.js' useRouter and returns a router whose push/replace methods
 * automatically prefix the current locale segment for non-default locales,
 * enabling locale-aware navigation without manually composing the path.
 */
export const useLocalizedRouter = () => {
  const router = useNextRouter()
  const { i18n } = useTranslation()
  const locale = i18n.language

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const push = (href: string, options?: any) => {
    let localizedHref = href
    if (locale && locale !== i18nConfig.defaultLocale) {
      if (href.startsWith('/') && !href.startsWith(`/${locale}`)) {
        localizedHref = `/${locale}${href}`
      }
    }
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    router.push(localizedHref as any, options)
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const replace = (href: string, options?: any) => {
    let localizedHref = href
    if (locale && locale !== i18nConfig.defaultLocale) {
      if (href.startsWith('/') && !href.startsWith(`/${locale}`)) {
        localizedHref = `/${locale}${href}`
      }
    }
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    router.replace(localizedHref as any, options)
  }

  // Forward other methods as needed, or just return the router object with overridden push/replace
  return { ...router, push, replace }
}
