'use client'

import { LocalizedLink as Link } from '@/components/ui'
import { setSidebar, toggleSidebar } from '@/store/slices'
import { useAppDispatch, useAppSelector } from '@/store/hooks'
import { useState, useEffect, useMemo } from 'react'
import { PanelLeftClose, X } from 'lucide-react'
import { usePathname } from 'next/navigation'
import { useTranslation } from '@/i18n'
import { navItems, NavItem, NavItemGroup } from '@/nav-items'
import { authUrls } from '@/auth-urls'
import { SidebarNavGroup } from './nav-group'
import { cn, isAllowed, isPathAvailable } from '@/lib/utils'
import Image from 'next/image'

/**
 * Type guard that narrows a {@link NavItem} | {@link NavItemGroup} union to {@link NavItemGroup} by checking for the `items` property.
 */
const isNavItemGroup = (item: NavItem | NavItemGroup): item is NavItemGroup => {
  return 'items' in item
}

/**
 * Client-side sidebar navigation: filters nav items by the current user's permissions, manages which group is open, and auto-expands the active parent on route change.
 *
 * From lg up it is a fixed column that the theme's `sidebar` flag collapses to an icon rail. The rail
 * stays narrow: hovering an entry opens a flyout beside it with the entry's name, or its submenu.
 * Below lg the same flag opens it as an off-canvas drawer, and every navigation closes it.
 */
export const Sidebar = () => {
  const dispatch = useAppDispatch()
  const { t } = useTranslation()
  const pathname = usePathname()
  const [currentMenu, setCurrentMenu] = useState<string>('')
  const sidebarFlag = useAppSelector((state) => state.theme.sidebar)
  const authState = useAppSelector((state) => state.auth)
  // On a desktop the flag means "collapsed to the rail".
  const compact = sidebarFlag

  const toggleMenu = (value: string) => {
    setCurrentMenu((oldValue) => {
      return oldValue === value ? '' : value
    })
  }

  const filteredNavItems = useMemo(() => {
    // Helper function to recursively filter children
    const filterNavItem = (item: NavItem): NavItem | undefined => {
      // Check if the item should be shown and if the user has permission
      if (item.show === false) {
        return undefined
      }

      // Check permissions from authUrls
      const authUrl = authUrls.find((u) => u.url === item.url)
      if (authUrl?.permissions && !isAllowed(authState, authUrl.permissions)) {
        return undefined
      }

      // A platform administrator acting in no tenant is not offered screens that need one
      if (!isPathAvailable(authState.user, item.url)) {
        return undefined
      }

      // If item has a submenu, recursively filter it. Hidden children (`show: false`) are kept as they are:
      // they are no submenu entries, only the pages the parent's link stays active on.
      if (item.children?.some((child) => child.show !== false)) {
        const filteredChildren = item.children.map(filterNavItem).filter((child): child is NavItem => child !== undefined)

        // If no submenu entries remain after filtering, don't show the parent
        if (filteredChildren.length === 0) {
          return undefined
        }

        return {
          ...item,
          children: [...filteredChildren, ...item.children.filter((child) => child.show === false)],
        }
      }

      return item
    }

    return navItems.reduce<(NavItem | NavItemGroup)[]>((filtered, item) => {
      if (isNavItemGroup(item)) {
        const filteredItems = item.items.map(filterNavItem).filter((groupItem): groupItem is NavItem => groupItem !== undefined)

        // Only include the group if it has at least one valid item
        if (filteredItems.length > 0) {
          filtered.push({
            ...item,
            items: filteredItems,
          })
        }
      } else {
        const filteredItem = filterNavItem(item)
        if (filteredItem !== undefined) {
          filtered.push(filteredItem)
        }
      }

      return filtered
    }, [])
  }, [authState])

  useEffect(() => {
    // Find and set active parent menu based on current path
    const path = pathname.replace(/^\/[a-z]{2}(\/|$)/, '/')
    filteredNavItems.forEach((group) => {
      const items = isNavItemGroup(group) ? group.items : [group]
      items.forEach((item) => {
        if (item.children?.some((child) => child.show !== false && child.url === path)) {
          setCurrentMenu(item.title)
        }
      })
    })
  }, [pathname, filteredNavItems])

  useEffect(() => {
    // The drawer closes on every navigation below lg; the desktop rail keeps its state.
    if (window.innerWidth < 1024) {
      dispatch(setSidebar(false))
    }
  }, [pathname, dispatch])

  return (
    <>
      {/* Drawer backdrop, below lg only */}
      <div
        className={cn('fixed inset-0 z-40 bg-overlay backdrop-blur-[2px] transition-opacity duration-200 lg:hidden', sidebarFlag ? 'opacity-100' : 'pointer-events-none opacity-0')}
        onClick={() => dispatch(toggleSidebar())}
        aria-hidden="true"
      />
      <nav
        className={cn(
          'fixed inset-y-0 inset-s-0 z-50 flex w-64 flex-col border-e border-border bg-surface transition-[width,transform,box-shadow] duration-200 ease-out',
          // Below lg: an off-canvas drawer
          sidebarFlag ? 'max-lg:translate-x-0 max-lg:shadow-lg' : 'max-lg:-translate-x-full max-lg:rtl:translate-x-full',
          // lg and up: full column, or the icon rail
          sidebarFlag && 'lg:w-18',
        )}
      >
        <div className={cn('flex h-14 shrink-0 items-center justify-between gap-2 px-4', compact && 'lg:justify-center lg:px-0')}>
          <Link href="/admin" className="flex min-w-0 items-center gap-2.5">
            <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-primary/10 ring-1 ring-primary/20">
              <Image className="size-5" src="/assets/images/icon.png" alt="logo" width={20} height={20} unoptimized priority />
            </span>
            <span className={cn('truncate text-[15px] font-semibold tracking-tight text-foreground', compact && 'lg:hidden')}>{t('brand.name')}</span>
          </Link>

          <button
            type="button"
            className="flex size-8 items-center justify-center rounded-md text-muted-foreground transition-colors hover:bg-surface-2 hover:text-foreground lg:hidden"
            onClick={() => dispatch(toggleSidebar())}
            aria-label={t('common.close')}
          >
            <X size={18} />
          </button>
          {!sidebarFlag && (
            <button
              type="button"
              className="hidden size-8 items-center justify-center rounded-md text-muted-foreground transition-colors hover:bg-surface-2 hover:text-foreground lg:flex rtl:rotate-180"
              onClick={() => dispatch(toggleSidebar())}
              aria-label={t('common.collapseSidebar')}
              title={t('common.collapseSidebar')}
            >
              <PanelLeftClose size={18} />
            </button>
          )}
        </div>

        <div className="relative flex-1 overflow-x-hidden overflow-y-auto px-3 pb-4">
          {filteredNavItems.map((group, index) => (
            <SidebarNavGroup key={`nav-group-${index}`} group={group} currentMenu={currentMenu} pathname={pathname} t={t} onToggleMenu={toggleMenu} compact={compact} />
          ))}
        </div>
      </nav>
    </>
  )
}
