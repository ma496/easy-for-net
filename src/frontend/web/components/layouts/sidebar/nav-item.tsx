'use client'

import { LocalizedLink as Link } from '@/components/ui'
import { NavItem } from '@/nav-items'
import AnimateHeight from 'react-animate-height'
import { ChevronDown } from 'lucide-react'

/**
 * Props for the {@link SidebarNavItem} component, providing the nav item definition and shared sidebar state used for active styling and expand/collapse behavior.
 */
interface NavItemProps {
  item: NavItem
  currentMenu: string
  pathname: string
  t: (key: string) => string
  onToggleMenu: (title: string) => void
}

/**
 * Renders a single sidebar entry: either a collapsible parent with a height-animated submenu of its visible children, or a plain link to the item's URL, active on that URL and on every page its hidden children name.
 */
/** Whether a nav url - which may hold a dynamic `{id}` segment - names the given locale-free pathname. */
const matchesUrl = (url: string, path: string) => url === path || (url.includes('{id}') && new RegExp(`^${url.replace('{id}', '[^/]+')}$`).test(path))

export const SidebarNavItem = ({ item, currentMenu, pathname, t, onToggleMenu }: NavItemProps) => {
  const submenu = item.children?.filter((child) => child.show !== false) ?? []

  if (submenu.length > 0) {
    return (
      <div className="nav-item">
        <button type="button" className={`${currentMenu === item.title ? 'active' : ''} group nav-link w-full`} onClick={() => onToggleMenu(item.title)}>
          <div className="flex items-center">
            {item.icon && <item.icon size={20} className="group-hover:text-primary!" />}
            <span className={`ps-3 text-black dark:text-[#506690] dark:group-hover:text-white-dark`}>{t(item.title)}</span>
          </div>

          <div className={currentMenu !== item.title ? '-rotate-90 rtl:rotate-90' : ''}>
            <ChevronDown size={16} />
          </div>
        </button>

        <AnimateHeight duration={300} height={currentMenu === item.title ? 'auto' : 0}>
          <ul className="sub-menu text-gray-500">
            {submenu.map((child, index) => (
              <li key={`${item.title}-child-${index}`}>
                {/* eslint-disable-next-line @typescript-eslint/no-explicit-any */}
                <Link href={child.url as any} className={`flex items-center ${pathname === child.url ? 'active' : ''}`}>
                  {child.icon ? <child.icon size={16} className="me-3" /> : <span className={`me-3`}>-</span>}
                  <span>{t(child.title)}</span>
                </Link>
              </li>
            ))}
          </ul>
        </AnimateHeight>
      </div>
    )
  }

  // A single link stays active on the pages its hidden children name (create, update, detail).
  const path = pathname.replace(/^\/[a-z]{2}(\/|$)/, '/')
  const isActive = matchesUrl(item.url, path) || (item.children?.some((child) => matchesUrl(child.url, path)) ?? false)

  return (
    <div className="nav-item">
      {/* eslint-disable-next-line @typescript-eslint/no-explicit-any */}
      <Link href={item.url as any} className={`group ${isActive ? 'active' : ''}`}>
        <div className="flex items-center">
          {item.icon && <item.icon size={20} className="group-hover:text-primary!" />}
          <span className={`text-black ltr:pl-3 rtl:pr-3 dark:text-[#506690] dark:group-hover:text-white-dark`}>{t(item.title)}</span>
        </div>
      </Link>
    </div>
  )
}
