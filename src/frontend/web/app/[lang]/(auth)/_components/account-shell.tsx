'use client'

import { ArrowLeft } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { LocalizedLink } from '@/components/ui'
import { BrandMark } from '../../_components/brand-mark'
import { SiteControls } from '../../_components/site-controls'

/**
 * Props for the {@link AccountShell} component.
 */
interface AccountShellProps {
  title: string
  description?: string
  /** Shows a back link to this route above the title; omitted where there is nowhere to go back to yet. */
  backHref?: string
  children: React.ReactNode
}

/**
 * Single-column frame for the signed-in account screens that live outside the admin shell (profile,
 * change password, tenant selection): a translucent top bar with the brand and the language and theme
 * switchers, then a `max-w-2xl` column with an optional back link, the title, and the page body.
 */
export const AccountShell = ({ title, description, backHref, children }: AccountShellProps) => {
  const { t } = useTranslation()

  return (
    <div className="relative min-h-screen bg-background">
      <div aria-hidden="true" className="pointer-events-none absolute inset-x-0 top-0 h-80 bg-[radial-gradient(ellipse_at_top,var(--color-primary)_0%,transparent_70%)] opacity-[0.07]" />

      <header className="sticky top-0 z-20 border-b border-border bg-background/80 backdrop-blur-md">
        <div className="mx-auto flex h-14 max-w-5xl items-center justify-between gap-3 px-4 sm:px-6">
          <BrandMark href={backHref ?? '/'} compactOnPhone={true} />
          <SiteControls />
        </div>
      </header>

      <main className="relative mx-auto w-full max-w-2xl animate-fade-up px-4 py-8 sm:px-6 sm:py-12">
        {backHref && (
          <LocalizedLink
            href={backHref}
            className="mb-4 inline-flex items-center gap-1.5 rounded-md text-sm font-medium text-muted-foreground transition-colors hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
          >
            <ArrowLeft className="size-4 rtl:-scale-x-100" />
            {t('common.back')}
          </LocalizedLink>
        )}
        <div className="mb-6">
          <h1 className="text-2xl font-semibold tracking-tight wrap-break-word text-foreground">{title}</h1>
          {description && <p className="mt-1.5 text-sm wrap-break-word text-muted-foreground">{description}</p>}
        </div>
        {children}
      </main>
    </div>
  )
}
