'use client'

import { useEffect, useState } from 'react'
import { RefreshCw, ServerOff } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { Button } from '@/components/ui'

/**
 * Full-page view displayed in place of the current route when the backend API
 * is unreachable. Retrying reloads the current URL and rebuilds normal app state.
 * Its strings resolve even with the API down, from the offline fallback in `i18n/types.ts`.
 */
export const ServiceUnavailableView = () => {
  const { t } = useTranslation()
  const [isRetrying, setIsRetrying] = useState(false)
  const title = t('error.serviceUnavailable.title')
  const brandName = t('brand.name')

  // The route's own metadata title names a page this screen has replaced (and, when the server
  // rendered it with the API already down, is an untranslated key), so the tab says what is shown.
  useEffect(() => {
    const previousTitle = document.title
    document.title = `${title} | ${brandName}`
    return () => {
      document.title = previousTitle
    }
  }, [title, brandName])

  const retry = () => {
    setIsRetrying(true)
    window.location.reload()
  }

  return (
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden bg-background px-6">
      <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_at_top,var(--color-primary)_0%,transparent_60%)] opacity-[0.08]" />
      <div className="relative w-full max-w-md animate-fade-up text-center">
        <div className="mx-auto mb-6 flex size-16 items-center justify-center rounded-2xl border border-border bg-surface shadow-sm">
          <ServerOff className="size-8 text-warning" strokeWidth={1.6} />
        </div>
        <span className="badge badge-warning mb-3">503</span>
        <h1 className="text-2xl font-semibold tracking-tight text-foreground sm:text-3xl">{title}</h1>
        <p className="mt-3 text-sm leading-relaxed text-muted-foreground sm:text-base">{t('error.serviceUnavailable.message')}</p>
        <Button type="button" size="lg" className="mt-8" icon={<RefreshCw className="size-4" />} isLoading={isRetrying} onClick={retry}>
          {t('error.serviceUnavailable.retry')}
        </Button>
      </div>
    </div>
  )
}
