'use client'

import { NavItem, NavItemGroup } from '@/nav-items'
import { SidebarNavItem } from './nav-item'
import { cn } from '@/lib/utils'

/**
 * Props for the {@link SidebarNavGroup} component, receiving the entry to render along with shared sidebar state (current menu, path, translator, toggle handler, rail mode).
 */
interface NavGroupProps {
  group: NavItem | NavItemGroup
  currentMenu: string
  pathname: string
  t: (key: string) => string
  onToggleMenu: (title: string) => void
  /** The desktop sidebar is the icon rail: labels give way to icons. */
  compact?: boolean
}

/**
 * Type guard that narrows a {@link NavItem} | {@link NavItemGroup} union to {@link NavItemGroup} by checking for the `items` property.
 */
const isNavItemGroup = (item: NavItem | NavItemGroup): item is NavItemGroup => {
  return 'items' in item
}

/**
 * Renders either a labeled group of sidebar nav items (with a small heading, a divider on the rail) or a single {@link SidebarNavItem} when the entry is a flat nav item.
 */
export const SidebarNavGroup = ({ group, currentMenu, pathname, t, onToggleMenu, compact }: NavGroupProps) => {
  if (isNavItemGroup(group)) {
    return (
      <div className="pt-5 first:pt-1">
        <h2 className={cn('mb-1.5 truncate px-2.5 text-[11px] font-semibold tracking-wider text-subtle-foreground uppercase rtl:tracking-normal', compact && 'lg:hidden')}>{t(group.title)}</h2>
        {compact && <div className="mx-auto mb-2 hidden h-px w-6 bg-border lg:block" />}
        <ul className="space-y-0.5">
          {group.items.map((item, index) => (
            <li key={`${group.title}-item-${index}`}>
              <SidebarNavItem item={item} currentMenu={currentMenu} pathname={pathname} t={t} onToggleMenu={onToggleMenu} compact={compact} />
            </li>
          ))}
        </ul>
      </div>
    )
  }

  return (
    <ul className="space-y-0.5 pt-1">
      <li>
        <SidebarNavItem item={group} currentMenu={currentMenu} pathname={pathname} t={t} onToggleMenu={onToggleMenu} compact={compact} />
      </li>
    </ul>
  )
}
