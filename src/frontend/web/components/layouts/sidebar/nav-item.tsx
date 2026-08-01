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
 * Renders a single sidebar entry: either a collapsible parent with a height-animated child submenu, or a plain link to the item's URL with active-state highlighting against the current pathname.
 */
export const SidebarNavItem = ({ item, currentMenu, pathname, t, onToggleMenu }: NavItemProps) => {
  if (item.children) {
    return (
      <div className="nav-item">
        <button type="button" className={`${currentMenu === item.title ? 'active' : ''} group nav-link w-full`} onClick={() => onToggleMenu(item.title)}>
          <div className="flex items-center">
            {item.icon && <item.icon size={20} className="group-hover:text-primary!" />}
            <span className={`text-black ps-3 dark:text-[#506690] dark:group-hover:text-white-dark`}>{t(item.title)}</span>
          </div>

          <div className={currentMenu !== item.title ? '-rotate-90 rtl:rotate-90' : ''}>
            <ChevronDown size={16} />
          </div>
        </button>

        <AnimateHeight duration={300} height={currentMenu === item.title ? 'auto' : 0}>
          <ul className="sub-menu text-gray-500">
            {item.children
              .filter((child) => child.show !== false)
              .map((child, index) => (
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

  return (
    <div className="nav-item">
      {/* eslint-disable-next-line @typescript-eslint/no-explicit-any */}
      <Link href={item.url as any} className={`group ${pathname === item.url ? 'active' : ''}`}>
        <div className="flex items-center">
          {item.icon && <item.icon size={20} className="group-hover:text-primary!" />}
          <span className={`text-black ltr:pl-3 rtl:pr-3 dark:text-[#506690] dark:group-hover:text-white-dark`}>{t(item.title)}</span>
        </div>
      </Link>
    </div>
  )
}
