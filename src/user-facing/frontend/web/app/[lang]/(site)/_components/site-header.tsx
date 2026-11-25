'use client'

import { useEffect, useRef, useState } from 'react'
import { usePathname } from 'next/navigation'
import { Menu, X } from 'lucide-react'
import { useTranslation, withoutLocale } from '@/i18n'
import { LocalizedLink } from '@/components/ui'
import { LogoMark, NavUser } from '@/components/custom'
import { useAppSelector } from '@/store/hooks'
import { cn } from '@/lib/utils'
import { SiteControls } from '../../_components/site-controls'

/** The public site's pages, in the order the header lists them. */
const SITE_LINKS = [
  { href: '/', key: 'site.nav.home', fallback: 'Home' },
  { href: '/about', key: 'site.nav.about', fallback: 'About' },
  { href: '/contact', key: 'site.nav.contact', fallback: 'Contact' },
] as const

const focusRing = 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
const iconButton = cn(
  'flex size-9 shrink-0 items-center justify-center rounded-full text-muted-foreground transition-colors hover:bg-surface-2 hover:text-foreground',
  focusRing,
)
/** The outlined pill the signed-out visitor's Sign in link is drawn as; it fills with the foreground on hover. */
const signInPill = cn(
  'h-10 items-center justify-center rounded-full border border-foreground px-5 text-sm font-medium whitespace-nowrap transition-colors hover:bg-foreground hover:text-background',
  focusRing,
)

/**
 * The public site's sticky header: the brand, the page links (the current one marked), the language and
 * theme switchers, and the visitor's account - a Sign in pill, or the account menu (which links to the
 * admin area for an account holding `Admin.Access`). Below `md` the page links fold into a menu button
 * that opens them as a panel under the bar; below `sm` the panel also holds the Sign in pill. The panel
 * closes on a link click, on Escape (focus goes back to the button) and on any route change. The account
 * area waits for the account info before it shows either state, so a signed-in visitor never sees
 * "Sign in" flash first; nothing else waits on it.
 */
export const SiteHeader = () => {
  const { t } = useTranslation()
  const pathname = withoutLocale(usePathname())
  // The menu is open on the path it was opened on, so navigating anywhere else closes it.
  const [menuOpenedOn, setMenuOpenedOn] = useState<string | null>(null)
  const isMenuOpen = menuOpenedOn === pathname
  const closeMenu = () => setMenuOpenedOn(null)
  const menuButtonRef = useRef<HTMLButtonElement>(null)
  const { user, isSettled } = useAppSelector((state) => state.auth)

  useEffect(() => {
    if (!isMenuOpen) return
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return
      setMenuOpenedOn(null)
      menuButtonRef.current?.focus()
    }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [isMenuOpen])

  const isActive = (href: string) => (href === '/' ? pathname === '/' : pathname === href || pathname.startsWith(`${href}/`))
  const navLabel = t('site.nav.label') || 'Main'
  const signInLabel = t('page.auth.signin.button') || 'Sign In'

  const links = (className: string, activeClassName: string) =>
    SITE_LINKS.map((link) => (
      <LocalizedLink
        key={link.href}
        href={link.href}
        aria-current={isActive(link.href) ? 'page' : undefined}
        onClick={closeMenu}
        className={cn(className, focusRing, 'transition-colors', isActive(link.href) ? activeClassName : 'text-foreground/80 hover:text-primary')}
      >
        {t(link.key) || link.fallback}
      </LocalizedLink>
    ))

  const isSignedOut = isSettled && !user

  return (
    <header className="sticky top-0 z-40 border-b border-border bg-surface/85 backdrop-blur-md">
      <div className="mx-auto flex h-16 max-w-7xl items-center justify-between gap-3 px-4 sm:h-20 md:px-8">
        <LocalizedLink href="/" className={cn('flex min-w-0 items-center gap-2 rounded-md', focusRing)}>
          <LogoMark />
          <span className="truncate text-[17px] font-medium tracking-tight text-foreground sm:text-[19px]">{t('brand.name') || 'Easy For Net'}</span>
        </LocalizedLink>

        <nav aria-label={navLabel} className="hidden items-center gap-6 text-[15px] whitespace-nowrap md:flex lg:gap-8">
          {links('rounded-md', 'font-medium text-foreground')}
        </nav>

        <div className="flex items-center gap-1.5">
          <SiteControls className="sm:ms-1" />
          {!isSettled ? (
            <span aria-hidden="true" className="ms-1 hidden size-9 animate-pulse rounded-full bg-surface-2 sm:block" />
          ) : user ? (
            <NavUser context="site" />
          ) : (
            <LocalizedLink href="/signin" className={cn(signInPill, 'ms-1 hidden sm:inline-flex')}>
              {signInLabel}
            </LocalizedLink>
          )}
          <button
            ref={menuButtonRef}
            type="button"
            className={cn(iconButton, 'md:hidden')}
            aria-expanded={isMenuOpen}
            aria-controls="site-mobile-nav"
            aria-label={t('common.menu') || 'Menu'}
            onClick={() => setMenuOpenedOn(isMenuOpen ? null : pathname)}
          >
            {isMenuOpen ? <X size={18} aria-hidden="true" /> : <Menu size={18} aria-hidden="true" />}
          </button>
        </div>
      </div>

      {isMenuOpen && (
        <nav
          id="site-mobile-nav"
          aria-label={navLabel}
          className="max-h-[calc(100dvh-4rem)] overflow-y-auto border-t border-border bg-background px-4 py-2 sm:max-h-[calc(100dvh-5rem)] md:hidden"
        >
          <div className="mx-auto flex max-w-7xl flex-col md:px-4">
            {links('rounded-md px-3 py-2.5 text-[15px]', 'bg-surface-2 font-medium text-foreground')}
            {isSignedOut && (
              <div className="flex flex-col sm:hidden">
                <hr className="my-2 border-border" />
                <LocalizedLink href="/signin" onClick={closeMenu} className={cn(signInPill, 'my-2 flex w-full')}>
                  {signInLabel}
                </LocalizedLink>
              </div>
            )}
          </div>
        </nav>
      )}
    </header>
  )
}
