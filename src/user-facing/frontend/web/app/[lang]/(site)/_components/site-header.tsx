'use client'

import { useEffect, useRef, useState } from 'react'
import { usePathname } from 'next/navigation'
import { Menu, X } from 'lucide-react'
import { useTranslation, withoutLocale } from '@/i18n'
import { LocalizedLink } from '@/components/ui'
import { NavUser } from '@/components/custom'
import { useAppSelector } from '@/store/hooks'
import { cn } from '@/lib/utils'
import { BrandMark } from '../../_components/brand-mark'
import { SiteControls } from '../../_components/site-controls'

/** The public site's pages, in the order the header lists them. */
const SITE_LINKS = [
  { href: '/', key: 'site.nav.home', fallback: 'Home' },
  { href: '/about', key: 'site.nav.about', fallback: 'About' },
  { href: '/contact', key: 'site.nav.contact', fallback: 'Contact' },
] as const

/**
 * The public site's sticky header: the brand, the page links (the current one marked), the language and
 * theme switchers, and the visitor's account - a Sign in link, or the account menu (which links to the
 * admin area for an account holding `Admin.Access`). Below `md` the page links fold into a menu button
 * that opens them as a panel under the bar; Escape closes it and puts focus back on the button. The
 * account area waits for the account info before it shows either state, so a signed-in visitor never
 * sees "Sign in" flash first, and it keeps one width throughout so the switchers beside it never move;
 * nothing else waits on it.
 */
export const SiteHeader = () => {
  const { t } = useTranslation()
  const pathname = withoutLocale(usePathname())
  const [isMenuOpen, setIsMenuOpen] = useState(false)
  const menuButtonRef = useRef<HTMLButtonElement>(null)
  const { user, isSettled } = useAppSelector((state) => state.auth)

  useEffect(() => {
    if (!isMenuOpen) return
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return
      setIsMenuOpen(false)
      menuButtonRef.current?.focus()
    }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [isMenuOpen])

  const isActive = (href: string) => (href === '/' ? pathname === '/' : pathname === href || pathname.startsWith(`${href}/`))

  const links = (className: string) =>
    SITE_LINKS.map((link) => (
      <LocalizedLink
        key={link.href}
        href={link.href}
        aria-current={isActive(link.href) ? 'page' : undefined}
        onClick={() => setIsMenuOpen(false)}
        className={cn(className, isActive(link.href) ? 'text-foreground' : 'text-muted-foreground hover:text-foreground')}
      >
        {t(link.key) || link.fallback}
      </LocalizedLink>
    ))

  return (
    <header className="sticky top-0 z-30 border-b border-border bg-background/80 backdrop-blur-md supports-[backdrop-filter]:bg-background/70">
      <div className="mx-auto flex h-16 max-w-6xl items-center gap-3 px-4 sm:px-6">
        <BrandMark compactOnPhone={true} />

        <nav aria-label={t('site.nav.label') || 'Main'} className="ms-6 hidden items-center gap-1 md:flex">
          {links('rounded-md px-3 py-2 text-sm font-medium transition-colors focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none')}
        </nav>

        <div className="ms-auto flex items-center gap-1.5">
          <SiteControls />
          {/* One box for every state - loading, signed in, signed out - so the switchers to its start never shift. */}
          <div className="flex min-w-24 shrink-0 items-center justify-end">
            {!isSettled ? (
              <span aria-hidden="true" className="size-9 animate-pulse rounded-full bg-surface-2" />
            ) : user ? (
              <NavUser context="site" />
            ) : (
              <LocalizedLink href="/signin" className="btn btn-primary btn-sm whitespace-nowrap">
                {t('page.auth.signin.button') || 'Sign In'}
              </LocalizedLink>
            )}
          </div>
          <button
            ref={menuButtonRef}
            type="button"
            className="flex size-9 items-center justify-center rounded-md text-muted-foreground transition-colors hover:bg-surface-2 hover:text-foreground md:hidden"
            aria-expanded={isMenuOpen}
            aria-controls="site-mobile-nav"
            aria-label={t('common.menu') || 'Menu'}
            onClick={() => setIsMenuOpen((open) => !open)}
          >
            {isMenuOpen ? <X size={18} /> : <Menu size={18} />}
          </button>
        </div>
      </div>

      {isMenuOpen && (
        <nav id="site-mobile-nav" aria-label={t('site.nav.label') || 'Main'} className="border-t border-border bg-background px-4 py-2 md:hidden">
          <div className="flex flex-col">{links('rounded-md px-3 py-2.5 text-sm font-medium')}</div>
        </nav>
      )}
    </header>
  )
}
