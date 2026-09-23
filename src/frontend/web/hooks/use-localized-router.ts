'use client'

import { useRouter as useNextRouter } from 'next/navigation'
import { useTranslation, i18nConfig } from '@/i18n'

/**
 * Wraps Next.js' useRouter and returns a router whose push/replace methods
 * automatically prefix the current locale segment for non-default locales,
 * enabling locale-aware navigation without manually composing the path.
 * `localize` exposes the same prefixing for callers that navigate outside the
 * router, such as a full page load.
 */
export const useLocalizedRouter = () => {
  const router = useNextRouter()
  const { i18n } = useTranslation()
  const locale = i18n.language

  const localize = (href: string) => {
    if (locale && locale !== i18nConfig.defaultLocale) {
      if (href.startsWith('/') && !href.startsWith(`/${locale}`)) {
        return `/${locale}${href}`
      }
    }
    return href
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const push = (href: string, options?: any) => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    router.push(localize(href) as any, options)
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const replace = (href: string, options?: any) => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    router.replace(localize(href) as any, options)
  }

  // Forward other methods as needed, or just return the router object with overridden push/replace
  return { ...router, push, replace, localize }
}
