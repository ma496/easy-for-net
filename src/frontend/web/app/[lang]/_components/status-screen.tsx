'use client'

import { ArrowLeft, type LucideIcon } from 'lucide-react'
import { useLocalizedRouter } from '@/hooks'
import { useTranslation } from '@/i18n'
import { Button } from '@/components/ui'
import { BrandMark } from './brand-mark'
import { SiteControls } from './site-controls'

/**
 * Props for the {@link StatusScreen} component.
 */
interface StatusScreenProps {
  /** The HTTP-style status shown as a badge above the title, e.g. 404. */
  code: number
  icon: LucideIcon
  title: string
  message: string
}

/**
 * Full-page status view (not found, forbidden) with the site's top bar, an icon tile, the status code,
 * a title and message, and a back button that returns one step in the browser history.
 */
export const StatusScreen = ({ code, icon: Icon, title, message }: StatusScreenProps) => {
  const router = useLocalizedRouter()
  const { t } = useTranslation()

  return (
    <div className="relative flex min-h-screen flex-col overflow-hidden bg-background text-foreground">
      <div aria-hidden="true" className="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_at_top,var(--color-primary)_0%,transparent_60%)] opacity-[0.08]" />

      <header className="relative flex items-center justify-between gap-3 px-4 py-4 sm:px-6">
        <BrandMark compactOnPhone={true} />
        <SiteControls />
      </header>

      <main className="relative flex flex-1 items-center justify-center px-6 pb-24">
        <div className="w-full max-w-md animate-fade-up text-center">
          <div className="mx-auto mb-6 flex size-16 items-center justify-center rounded-2xl border border-border bg-surface shadow-xs">
            <Icon className="size-8 text-danger" strokeWidth={1.6} />
          </div>
          <span className="badge badge-danger mb-3 tabular-nums">{code}</span>
          <h1 className="text-2xl font-semibold tracking-tight wrap-break-word sm:text-3xl">{title}</h1>
          <p className="mt-3 text-sm leading-relaxed wrap-break-word text-muted-foreground sm:text-base">{message}</p>
          <Button type="button" variant="outline" size="lg" className="mt-8" icon={<ArrowLeft className="size-4 rtl:-scale-x-100" />} onClick={() => router.back()}>
            {t('common.back')}
          </Button>
        </div>
      </main>
    </div>
  )
}
