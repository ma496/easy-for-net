'use client'

import { LocalizedLink as Link } from '@/components/ui'
import { NavItem } from '@/nav-items'
import AnimateHeight from 'react-animate-height'
import { ChevronRight } from 'lucide-react'
import { cn } from '@/lib/utils'
import { RailFlyout } from './rail-flyout'

/**
 * Props for the {@link SidebarNavItem} component, providing the nav item definition and shared sidebar state used for active styling and expand/collapse behavior.
 */
interface NavItemProps {
  item: NavItem
  currentMenu: string
  pathname: string
  t: (key: string) => string
  onToggleMenu: (title: string) => void
  /** The desktop sidebar is the icon rail: only the icon shows, and hovering it opens a flyout with the name or the submenu. */
  compact?: boolean
}

/** Whether a nav url - which may hold a dynamic `{id}` segment - names the given locale-free pathname. */
const matchesUrl = (url: string, path: string) => url === path || (url.includes('{id}') && new RegExp(`^${url.replace('{id}', '[^/]+')}$`).test(path))

const rowClass = 'group flex h-9 w-full items-center gap-3 rounded-md px-2.5 text-[13.5px] font-medium transition-colors duration-150'
const idleClass = 'text-muted-foreground hover:bg-surface-2 hover:text-foreground'
const activeClass = 'bg-primary/10 text-primary'
const labelClass = 'rounded-md bg-foreground px-2.5 py-1.5 text-xs font-medium whitespace-nowrap text-background shadow-md'

/**
 * Renders a single sidebar entry: either a collapsible parent with a height-animated submenu of its visible children, or a plain link to the item's URL, active on that URL and on every page its hidden children name.
 * On the icon rail the submenu opens as a flyout beside the parent instead, and a plain link names itself in a flyout label.
 */
export const SidebarNavItem = ({ item, currentMenu, pathname, t, onToggleMenu, compact = false }: NavItemProps) => {
  const submenu = item.children?.filter((child) => child.show !== false) ?? []
  const path = pathname.replace(/^\/[a-z]{2}(\/|$)/, '/')

  if (submenu.length > 0) {
    const isOpen = currentMenu === item.title
    const hasActiveChild = item.children?.some((child) => matchesUrl(child.url, path)) ?? false

    const submenuLinks = (flyout: boolean, onNavigate?: () => void) =>
      submenu.map((child, index) => {
        const isActive = matchesUrl(child.url, path)
        return (
          <li key={`${item.title}-child-${index}`}>
            <Link
              // eslint-disable-next-line @typescript-eslint/no-explicit-any
              href={child.url as any}
              onClick={onNavigate}
              className={cn(
                'relative flex h-8 items-center gap-2 rounded-md px-2.5 text-[13px] transition-colors',
                isActive && flyout && 'bg-primary/10 font-medium text-primary',
                isActive && !flyout && 'font-medium text-primary before:absolute before:-inset-s-3.25 before:top-1.5 before:bottom-1.5 before:w-0.5 before:rounded-full before:bg-primary',
                !isActive && 'text-muted-foreground hover:bg-surface-2 hover:text-foreground',
              )}
            >
              {child.icon && <child.icon size={15} className="shrink-0" />}
              <span className="truncate">{t(child.title)}</span>
            </Link>
          </li>
        )
      })

    return (
      <div>
        <RailFlyout
          enabled={compact}
          interactive
          openOnClick
          content={(close) => (
            <div className="w-56 rounded-lg border border-border bg-surface p-1.5 shadow-lg">
              <p className="truncate px-2.5 pt-1 pb-1.5 text-xs font-semibold text-foreground">{t(item.title)}</p>
              <ul className="space-y-0.5">{submenuLinks(true, close)}</ul>
            </div>
          )}
        >
          {(flyoutOpen) => (
            <button
              type="button"
              aria-label={compact ? t(item.title) : undefined}
              className={cn(
                rowClass,
                hasActiveChild ? 'text-foreground hover:bg-surface-2' : idleClass,
                compact && 'lg:justify-center lg:px-0',
                compact && hasActiveChild && 'lg:bg-primary/10 lg:text-primary',
                flyoutOpen && !hasActiveChild && 'bg-surface-2 text-foreground',
              )}
              // On the rail the flyout is the submenu; the inline one is left as it was for when the sidebar expands.
              onClick={() => !compact && onToggleMenu(item.title)}
              aria-haspopup={compact ? 'menu' : undefined}
              aria-expanded={compact ? flyoutOpen : isOpen}
            >
              {item.icon && <item.icon size={18} className={cn('shrink-0', hasActiveChild ? 'text-primary' : 'text-subtle-foreground group-hover:text-foreground')} />}
              <span className={cn('flex-1 truncate text-start', compact && 'lg:hidden')}>{t(item.title)}</span>
              <ChevronRight size={15} className={cn('shrink-0 text-subtle-foreground transition-transform duration-200 rtl:rotate-180', isOpen && 'rotate-90 rtl:rotate-90', compact && 'lg:hidden')} />
            </button>
          )}
        </RailFlyout>

        <AnimateHeight duration={200} height={isOpen && !compact ? 'auto' : 0}>
          <ul className="ms-[1.15rem] mt-0.5 space-y-0.5 border-s border-border py-0.5 ps-3">{submenuLinks(false)}</ul>
        </AnimateHeight>
      </div>
    )
  }

  // A single link stays active on the pages its hidden children name (create, update, detail).
  const isActive = matchesUrl(item.url, path) || (item.children?.some((child) => matchesUrl(child.url, path)) ?? false)

  return (
    <RailFlyout enabled={compact} content={() => <div className={labelClass}>{t(item.title)}</div>}>
      {() => (
        <Link
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          href={item.url as any}
          aria-label={compact ? t(item.title) : undefined}
          className={cn(rowClass, isActive ? activeClass : idleClass, compact && 'lg:justify-center lg:px-0')}
        >
          {item.icon && <item.icon size={18} className={cn('shrink-0', isActive ? 'text-primary' : 'text-subtle-foreground group-hover:text-foreground')} />}
          <span className={cn('truncate', compact && 'lg:hidden')}>{t(item.title)}</span>
        </Link>
      )}
    </RailFlyout>
  )
}
