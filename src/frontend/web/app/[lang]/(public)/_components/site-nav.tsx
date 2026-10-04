'use client'

import { ArrowRight } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { useAppSelector } from '@/store/hooks'
import { LocalizedLink } from '@/components/ui'
import { BrandMark } from '../../_components/brand-mark'
import { SiteControls } from '../../_components/site-controls'
import { REPOSITORY_URL } from './links'

/**
 * Sticky, translucent top navigation for the public landing page: the brand, in-page and source links,
 * the language and theme switchers, and a call to action that reads the auth state to offer either
 * sign-in or the admin dashboard.
 */
export const SiteNav = () => {
  const { t } = useTranslation()
  const isAuthenticated = useAppSelector((s) => s.auth.isAuthenticated)

  return (
    <header className="sticky top-0 z-30 border-b border-border/70 bg-background/75 backdrop-blur-md">
      <div className="mx-auto flex h-16 max-w-6xl items-center justify-between gap-3 px-4 sm:px-6">
        <div className="flex min-w-0 items-center gap-6">
          <BrandMark />
          <nav className="hidden items-center gap-1 md:flex">
            <a href="#features" className="rounded-md px-3 py-1.5 text-sm text-muted-foreground transition-colors hover:bg-surface-2 hover:text-foreground">
              {t('page.home.features.titleBadge')}
            </a>
            <LocalizedLink
              href={REPOSITORY_URL}
              target="_blank"
              rel="noopener noreferrer"
              className="rounded-md px-3 py-1.5 text-sm text-muted-foreground transition-colors hover:bg-surface-2 hover:text-foreground"
            >
              {t('page.home.hero.viewSource')}
            </LocalizedLink>
          </nav>
        </div>

        <div className="flex shrink-0 items-center gap-1.5">
          <SiteControls />
          <LocalizedLink href={isAuthenticated ? '/admin' : '/signin'} className="btn btn-primary btn-sm ms-1.5 hidden sm:inline-flex">
            {isAuthenticated ? t('page.home.hero.dashboardDetail') : t('page.home.hero.signin')}
            <ArrowRight className="size-3.5 rtl:-scale-x-100" />
          </LocalizedLink>
        </div>
      </div>
    </header>
  )
}
