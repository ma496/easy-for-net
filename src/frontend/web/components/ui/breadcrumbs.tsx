'use client'

import { NavItem, NavItemGroup, navItems } from '@/nav-items'
import { LocalizedLink as Link } from './localized-link'
import { usePathname } from 'next/navigation'
import { useTranslation } from '@/i18n'

/**
 * Walks the nav-items tree and returns the chain of NavItems leading to (and including) the item matching the given pathname, supporting dynamic `{id}` segments.
 */
const findActivePathItems = (items: (NavItem | NavItemGroup)[], pathname: string): NavItem[] => {
  const result: NavItem[] = []

  const findInItems = (items: NavItem[], currentPath: string) => {
    for (const item of items) {
      if (item.url === currentPath || (item.url.includes('{id}') && currentPath.match(new RegExp(item.url.replace('{id}', '[^/]+'))))) {
        result.push(item)
        return true
      }
      if (item.children) {
        if (findInItems(item.children, currentPath)) {
          result.unshift(item)
          return true
        }
      }
    }
    return false
  }

  const flatItems = items.reduce<NavItem[]>((acc, item) => {
    if ('items' in item) {
      return [...acc, ...item.items]
    }
    return [...acc, item]
  }, [])

  findInItems(flatItems, pathname)
  return result
}

/** Props for the Breadcrumbs component, accepting optional extra styling on the list element. */
interface BreadcrumbsProps {
  className?: string
}

/**
 * Breadcrumbs renders the navigation trail for the current path, using a localized link to "/admin" as the home entry and translating each segment's title from the nav-items configuration.
 * It is a client component that reads the current pathname with usePathname.
 */
export function Breadcrumbs({ className }: BreadcrumbsProps) {
  const pathname = usePathname()
  // get the path items based on pathname, support dynamic route like /admin/items/{id}
  const normalizedPathname = pathname.replace(/^\/[a-z]{2}(\/|$)/, '/')
  const activePathItems = findActivePathItems(navItems, normalizedPathname)
  const { t } = useTranslation()

  return (
    <ul className={`flex gap-2 ${className || ''}`}>
      <li>
        <Link href="/admin" className="text-primary hover:underline">
          {t('navigation.home')}
        </Link>
      </li>
      {activePathItems.map((item, index) => (
        <li key={item.url} className="before:content-['/'] before:me-2">
          {index === activePathItems.length - 1 ? (
            <span>{t(item.title)}</span>
          ) : (
            /* eslint-disable-next-line @typescript-eslint/no-explicit-any */
            <Link href={item.url as any} className="text-primary hover:underline">
              {t(item.title)}
            </Link>
          )}
        </li>
      ))}
    </ul>
  )
}
