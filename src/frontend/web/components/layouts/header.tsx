'use client'
import { toggleSidebar } from '@/store/slices'
import { ThemeChanger, NavUser, LanguageDropdown, TenantSwitcher } from '@/components/custom'
import { Breadcrumbs, LocalizedLink } from '@/components/ui'
import Image from 'next/image'
import { useAppDispatch, useAppSelector } from '@/store/hooks'
import { SearchComponent } from './search-component'
import { PanelLeft } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { NotificationBell } from '@/components/notifications'
import { useNotificationHub } from '@/hooks'
import { isPlatformWithoutTenant } from '@/lib/utils'

/**
 * Header is the sticky, translucent top bar of the admin shell: the sidebar toggle (drawer below lg, rail from lg up) and the breadcrumb trail on the start side; search, tenant switcher, notifications, theme, language and the user menu on the end side. It also subscribes to the notification hub.
 */
export const Header = () => {
  const dispatch = useAppDispatch()
  const theme = useAppSelector((state) => state.theme.theme)
  // Notifications are read in the tenant being acted in, or in platform scope by a platform account acting in none. An ordinary account left with no tenant is sent to choose one, so the bell waits until it has.
  const canReadNotifications = useAppSelector((state) => state.auth.activeTenant != null || isPlatformWithoutTenant(state.auth.user))
  const { t } = useTranslation()

  useNotificationHub()

  return (
    <header className="sticky top-0 z-30 border-b border-border bg-background/80 backdrop-blur-md supports-[backdrop-filter]:bg-background/70">
      <div className="flex h-14 items-center gap-2 px-3 sm:px-5 lg:px-6">
        <button
          type="button"
          className="flex size-9 shrink-0 items-center justify-center rounded-md text-muted-foreground transition-colors hover:bg-surface-2 hover:text-foreground rtl:-scale-x-100"
          onClick={() => dispatch(toggleSidebar())}
          aria-label={t('common.menu')}
        >
          <PanelLeft size={18} />
        </button>
        <LocalizedLink href="/admin" className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-primary/10 ring-1 ring-primary/20 lg:hidden" aria-label={t('brand.name')}>
          <Image className="size-5" src="/assets/images/icon.png" alt="" width={20} height={20} unoptimized />
        </LocalizedLink>
        <div className="hidden h-5 w-px bg-border sm:block" />
        <Breadcrumbs className="hidden min-w-0 flex-1 sm:flex" />
        <div className="flex-1 sm:hidden" />

        <div className="flex items-center gap-1 sm:gap-1.5">
          <SearchComponent />
          <TenantSwitcher />
          {canReadNotifications && <NotificationBell />}
          <ThemeChanger theme={theme} />
          <LanguageDropdown onlyFlag={true} />
          <div className="ms-1">
            <NavUser />
          </div>
        </div>
      </div>
    </header>
  )
}
