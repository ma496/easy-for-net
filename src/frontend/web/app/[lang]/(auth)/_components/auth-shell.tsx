'use client'

import { Check } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { BackLink } from '@/components/custom'
import { BrandMark } from '../../_components/brand-mark'
import { SiteControls } from '../../_components/site-controls'

/**
 * Props for the {@link AuthShell} component.
 */
interface AuthShellProps {
  /** The page heading above the form; omitted by screens that title their own states (verify email). */
  title?: string
  description?: string
  /** Shows a back link, stepping one entry back in history, in place of the brand mark at the top start. */
  showBack?: boolean
  children: React.ReactNode
}

/**
 * Split frame for the signed-out auth screens. From `lg` up a brand panel fills the start half (logo,
 * value line, a few product highlights, over a soft accent glow) and the form sits centred in the other;
 * below `lg` only the form remains, on a card. Language and theme switchers sit in the top end corner.
 */
export const AuthShell = ({ title, description, showBack = false, children }: AuthShellProps) => {
  const { t } = useTranslation()

  const highlights = [
    t('page.home.features.items.permissions.title'),
    t('page.home.features.items.users.title'),
    t('page.home.features.items.jobs.title'),
  ]

  return (
    <div className="grid min-h-screen bg-background lg:grid-cols-2">
      <aside className="relative hidden overflow-hidden border-e border-border bg-surface p-10 lg:flex lg:flex-col lg:justify-between xl:p-14">
        <div aria-hidden="true" className="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_at_top,var(--color-primary)_0%,transparent_65%)] opacity-[0.14]" />
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-0 bg-[linear-gradient(to_right,var(--color-border)_1px,transparent_1px),linear-gradient(to_bottom,var(--color-border)_1px,transparent_1px)] bg-size-[40px_40px] opacity-50 mask-[radial-gradient(ellipse_at_top,black_10%,transparent_70%)]"
        />

        <BrandMark className="relative self-start" />

        <div className="relative max-w-md">
          <span className="inline-flex items-center gap-2 rounded-full border border-border bg-surface/80 px-3 py-1 text-xs font-medium text-muted-foreground">
            <span className="size-1.5 rounded-full bg-primary" />
            {t('page.home.hero.badge')}
          </span>
          <h2 className="mt-6 text-4xl font-semibold tracking-tight text-foreground xl:text-5xl">
            {t('page.home.hero.titleFocus')} <span className="text-primary">{t('page.home.hero.titleHighlight')}</span>
          </h2>
          <p className="mt-4 text-base leading-relaxed text-muted-foreground">{t('page.home.hero.description')}</p>
          <ul className="mt-8 space-y-3">
            {highlights.map((highlight) => (
              <li key={highlight} className="flex items-center gap-3 text-sm text-foreground">
                <span className="flex size-6 shrink-0 items-center justify-center rounded-md bg-primary/10 text-primary">
                  <Check className="size-3.5" strokeWidth={2.5} />
                </span>
                {highlight}
              </li>
            ))}
          </ul>
        </div>

        <p className="relative text-xs text-subtle-foreground">
          © {new Date().getFullYear()} {t('brand.name')}. {t('common.allRightsReserved')}
        </p>
      </aside>

      <main className="flex min-h-screen min-w-0 flex-col">
        <header className="flex items-center justify-between gap-3 px-4 py-4 sm:px-6">
          {showBack ? <BackLink label={t('common.back')} /> : <BrandMark className="lg:invisible" />}
          <SiteControls />
        </header>

        <div className="flex flex-1 items-center justify-center px-4 pt-4 pb-16 sm:px-6">
          <div className="w-full max-w-sm animate-fade-up rounded-2xl border border-border bg-surface p-6 shadow-xs sm:p-8 lg:max-w-sm lg:rounded-none lg:border-0 lg:bg-transparent lg:p-0 lg:shadow-none">
            {title && (
              <div className="mb-8">
                <h1 className="text-2xl font-semibold tracking-tight wrap-break-word text-foreground">{title}</h1>
                {description && <p className="mt-1.5 text-sm wrap-break-word text-muted-foreground">{description}</p>}
              </div>
            )}
            {children}
          </div>
        </div>
      </main>
    </div>
  )
}
