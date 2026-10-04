'use client'
import { LocalizedLink } from '@/components/ui'
import { ArrowRight, Code2 } from 'lucide-react'
import { useAppSelector } from '@/store/hooks'
import { useTranslation } from '@/i18n'
import { REPOSITORY_URL } from './links'

/**
 * Interactive client-side hero section for the public landing page.
 * Reads the auth state to swap the primary call-to-action between sign-in and admin dashboard, and renders the headline, description, and supporting GitHub link,
 * over a faint grid and accent glow, followed by a decorative preview of the admin app.
 */
export const Hero = () => {
  const { t } = useTranslation()
  const authState = useAppSelector(s => s.auth)

  return (
    <section className="relative isolate overflow-hidden">
      <div aria-hidden="true" className="pointer-events-none absolute inset-0 -z-10">
        <div className="absolute inset-0 bg-[linear-gradient(to_right,var(--color-border)_1px,transparent_1px),linear-gradient(to_bottom,var(--color-border)_1px,transparent_1px)] bg-size-[48px_48px] opacity-60 mask-[radial-gradient(ellipse_70%_60%_at_50%_0%,black_20%,transparent_75%)]" />
        <div className="absolute inset-x-0 top-0 h-[36rem] bg-[radial-gradient(ellipse_at_top,var(--color-primary)_0%,transparent_65%)] opacity-[0.16]" />
      </div>

      <div className="mx-auto max-w-6xl px-4 pt-16 pb-16 text-center sm:px-6 sm:pt-24 lg:pt-28">
        <div className="inline-flex max-w-full items-center gap-2 rounded-full border border-border bg-surface/80 px-3 py-1 text-xs font-medium text-muted-foreground backdrop-blur-sm sm:text-sm">
          <span className="size-1.5 shrink-0 rounded-full bg-primary" />
          <span className="truncate">{t('page.home.hero.badge')}</span>
        </div>

        <h1 className="mx-auto mt-6 max-w-4xl text-4xl font-semibold tracking-tight wrap-break-word text-foreground sm:text-6xl lg:text-7xl">
          <span className="block">{t('page.home.hero.titleFocus')}</span>
          <span className="block text-primary">{t('page.home.hero.titleHighlight')}</span>
        </h1>

        <p className="mx-auto mt-6 max-w-2xl text-base leading-relaxed text-muted-foreground sm:text-lg">
          {t('page.home.hero.description')}
        </p>

        <div className="mt-10 flex flex-col items-stretch justify-center gap-3 sm:flex-row sm:items-center">
          {
            authState.isAuthenticated ? (
              <LocalizedLink href="/admin" className="btn btn-primary btn-lg">
                {t('page.home.hero.dashboardDetail')}
                <ArrowRight className="size-4 rtl:-scale-x-100" />
              </LocalizedLink>
            ) : (
              <LocalizedLink href="/signin" className="btn btn-primary btn-lg">
                {t('page.home.hero.signin')}
                <ArrowRight className="size-4 rtl:-scale-x-100" />
              </LocalizedLink>
            )
          }
          <LocalizedLink href={REPOSITORY_URL} target="_blank" rel="noopener noreferrer" className="btn btn-secondary btn-lg">
            <Code2 className="size-4" />
            {t('page.home.hero.viewSource')}
          </LocalizedLink>
        </div>

        <AppPreview />
      </div>
    </section>
  )
}

/**
 * A purely decorative, text-free sketch of the admin app (window bar, sidebar, stat cards and table rows)
 * drawn from surface tokens, so it reads correctly in light and dark mode. Hidden from assistive technology.
 */
const AppPreview = () => (
  <div aria-hidden="true" className="mx-auto mt-16 max-w-5xl rounded-2xl border border-border bg-surface/60 p-2 shadow-xs backdrop-blur-sm sm:mt-20">
    <div className="overflow-hidden rounded-xl border border-border bg-background text-start">
      <div className="flex items-center gap-1.5 border-b border-border bg-surface px-4 py-3">
        <span className="size-2.5 rounded-full bg-surface-3" />
        <span className="size-2.5 rounded-full bg-surface-3" />
        <span className="size-2.5 rounded-full bg-surface-3" />
        <span className="ms-4 h-2 w-40 rounded-full bg-surface-2" />
      </div>
      <div className="flex">
        <div className="hidden w-48 shrink-0 space-y-2 border-e border-border bg-surface p-4 sm:block">
          <div className="mb-5 flex items-center gap-2">
            <span className="size-5 rounded-md bg-primary/20" />
            <span className="h-2 w-20 rounded-full bg-surface-3" />
          </div>
          <div className="flex items-center gap-2 rounded-md bg-primary/10 px-2 py-1.5">
            <span className="size-3 rounded-sm bg-primary/60" />
            <span className="h-2 w-16 rounded-full bg-primary/40" />
          </div>
          {[24, 20, 28, 16, 22].map((width, index) => (
            <div key={index} className="flex items-center gap-2 px-2 py-1.5">
              <span className="size-3 rounded-sm bg-surface-3" />
              <span className="h-2 rounded-full bg-surface-2" style={{ width: `${width * 4}px` }} />
            </div>
          ))}
        </div>
        <div className="min-w-0 flex-1 space-y-4 p-4 sm:p-6">
          <div className="h-3 w-32 rounded-full bg-surface-3" />
          <div className="grid grid-cols-3 gap-3">
            {['bg-primary/50', 'bg-success/50', 'bg-warning/50'].map((accent) => (
              <div key={accent} className="rounded-lg border border-border bg-surface p-3">
                <span className="block h-2 w-10 rounded-full bg-surface-3" />
                <span className="mt-3 block h-4 w-14 rounded-md bg-surface-2" />
                <span className={`mt-3 block h-1 w-full rounded-full ${accent}`} />
              </div>
            ))}
          </div>
          <div className="overflow-hidden rounded-lg border border-border bg-surface">
            <div className="flex gap-4 border-b border-border bg-surface-2 px-3 py-2.5">
              <span className="h-2 w-16 rounded-full bg-surface-3" />
              <span className="h-2 w-24 rounded-full bg-surface-3" />
              <span className="ms-auto h-2 w-10 rounded-full bg-surface-3" />
            </div>
            {[0, 1, 2, 3].map((row) => (
              <div key={row} className="flex items-center gap-4 border-b border-border px-3 py-3 last:border-b-0">
                <span className="size-5 shrink-0 rounded-full bg-surface-2" />
                <span className="h-2 w-20 rounded-full bg-surface-3" />
                <span className="hidden h-2 w-28 rounded-full bg-surface-2 sm:block" />
                <span className={`ms-auto h-4 w-12 rounded-full ${row % 2 === 0 ? 'bg-success/15' : 'bg-primary/15'}`} />
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  </div>
)
