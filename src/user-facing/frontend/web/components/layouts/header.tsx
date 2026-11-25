'use client'
import { toggleSidebar } from '@/store/slices'
import { ThemeChanger, NavUser, LanguageDropdown, LogoMark } from '@/components/custom'
import { Breadcrumbs, LocalizedLink } from '@/components/ui'
import { useAppDispatch, useAppSelector } from '@/store/hooks'
import { SearchComponent } from './search-component'
import { PanelLeft } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { NotificationBell } from '@/components/notifications'
import { useNotificationHub } from '@/hooks'
import { adminPath } from '@/lib/paths'

/**
 * Header is the sticky, translucent top bar of the admin shell: the sidebar toggle (drawer below lg, rail from lg up) and the breadcrumb trail on the start side; search, notifications, theme, language and the user menu on the end side. It also subscribes to the notification hub.
 */
export const Header = () => {
  const dispatch = useAppDispatch()
  const theme = useAppSelector((state) => state.theme.theme)
  const canReadNotifications = useAppSelector((state) => state.auth.user != null)
  const { t } = useTranslation()

  useNotificationHub()

  return (
    <header className="sticky top-0 z-30 border-b border-border bg-background/80 backdrop-blur-md supports-[backdrop-filter]:bg-background/70">
      <div className="flex h-14 items-center gap-2 px-3 sm:px-5 lg:px-6">
        <button
          type="button"
          className="flex size-9 shrink-0 items-center justify-center rounded-md text-muted-foreground transition-colors hover:bg-surface-2 hover:text-foreground rtl:-scale-x-100"
          onClick={() => dispatch(toggleSidebar())}
          aria-label={t('common.menu') || 'Menu'}
        >
          <PanelLeft size={18} />
        </button>
        <LocalizedLink href={adminPath()} className="flex size-8 shrink-0 lg:hidden" aria-label={t('brand.name') || 'Easy For Net'}>
          <LogoMark />
        </LocalizedLink>
        <div className="hidden h-5 w-px bg-border sm:block" />
        <Breadcrumbs className="hidden min-w-0 flex-1 sm:flex" />
        <div className="flex-1 sm:hidden" />

        <div className="flex items-center gap-1 sm:gap-1.5">
          <SearchComponent />
          {canReadNotifications && <NotificationBell />}
          <ThemeChanger theme={theme} />
          <LanguageDropdown onlyFlag={true} />
          <div className="ms-1">
            <NavUser context="admin" />
          </div>
        </div>
      </div>
    </header>
  )
}
