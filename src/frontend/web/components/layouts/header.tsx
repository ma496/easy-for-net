'use client'
import { useEffect } from 'react'
import { LocalizedLink } from '@/components/ui/localized-link'
import { toggleSidebar } from '@/store/slices/themeConfigSlice'
import { usePathname } from 'next/navigation'
import { ThemeChanger } from '../custom/theme-changer'
import { NavUser } from '../custom/nav-user'
import { useAppDispatch, useAppSelector } from '@/store/hooks'
import { SearchComponent } from './search-component'
import { Menu } from 'lucide-react'
import { LanguageDropdown } from '../custom/language-dropdown'
import { useTranslation } from '@/i18n'
import { NotificationBell } from '../notifications/notification-bell'
import { useNotificationHub } from '@/hooks/use-notification-hub'

/**
 * Header is the client-side application top bar that contains the brand logo, mobile sidebar toggle, search box, notification bell, theme changer, language dropdown, and the user navigation menu; it also activates the current horizontal-menu link and subscribes to the notification hub.
 */
export const Header = () => {
  const pathname = usePathname()
  const dispatch = useAppDispatch()
  const themeConfig = useAppSelector((state) => state.theme)
  const { t } = useTranslation()

  useNotificationHub()

  useEffect(() => {
    const selector = document.querySelector('ul.horizontal-menu a[href="' + window.location.pathname + '"]')
    if (selector) {
      const all = document.querySelectorAll('ul.horizontal-menu .nav-link.active')
      for (let i = 0; i < all.length; i++) {
        all[0]?.classList.remove('active')
      }

      const allLinks = document.querySelectorAll('ul.horizontal-menu a.active')
      for (let i = 0; i < allLinks.length; i++) {
        const element = allLinks[i]
        element?.classList.remove('active')
      }
      selector?.classList.add('active')

      const ul = selector.closest('ul.sub-menu')
      if (ul) {
        const ele = ul.closest('li.menu')?.querySelectorAll('.nav-link')
        if (ele && ele.length > 0) {
          setTimeout(() => {
            ele[0]?.classList.add('active')
          })
        }
      }
    }
  }, [pathname])

  return (
    <header className={`z-40 ${themeConfig.semidark && themeConfig.menu === 'horizontal' ? 'dark' : ''}`}>
      <div className="shadow-xs">
        <div className="relative flex w-full items-center bg-white px-5 py-2.5 dark:bg-black">
          <div className="ms-2 horizontal-logo flex items-center justify-between lg:hidden">
            <LocalizedLink href="/admin" className="flex shrink-0 items-center main-logo">
              <img className="-ms-1 inline w-8" src="/assets/images/icon.png" alt="logo" />
              <span className="ms-1.5 hidden align-middle text-sm font-semibold transition-all duration-300 md:inline dark:text-white-light">{t('brand.name')}</span>
            </LocalizedLink>
            <button
              type="button"
              className="ms-2 collapse-icon flex flex-none cursor-pointer rounded-full bg-white-light/40 p-2 hover:bg-white-light/90 hover:text-primary lg:hidden dark:bg-dark/40 dark:text-[#d0d2d6] dark:hover:bg-dark/60 dark:hover:text-primary"
              onClick={() => dispatch(toggleSidebar())}
            >
              <Menu className="h-5 w-5" />
            </button>
          </div>

          <div className="ms-auto flex items-center gap-1.5 sm:ms-0 sm:flex-1 lg:gap-2 dark:text-[#d0d2d6]">
            <div className="ms-2 hidden sm:me-auto sm:block">
              <SearchComponent />
            </div>

            <div className="flex items-center justify-center gap-2">
              <div>
                <NotificationBell />
              </div>

              <div>
                <ThemeChanger theme={themeConfig.theme} />
              </div>

              <div>
                <LanguageDropdown onlyFlag={true} />
              </div>

              <div>
                <NavUser />
              </div>
            </div>
          </div>
        </div>
      </div>
    </header>
  )
}

