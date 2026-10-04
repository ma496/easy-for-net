'use client'

import { NavItem, NavItemGroup, navItems } from '@/nav-items'
import { authUrls } from '@/auth-urls'
import { LocalizedLink as Link } from './localized-link'
import { usePathname } from 'next/navigation'
import { useTranslation } from '@/i18n'
import { useAppSelector } from '@/store/hooks'
import { AuthState, cn, isAllowed, isPathAvailable } from '@/lib/utils'
import { ChevronRight } from 'lucide-react'

/**
 * Walks the nav-items tree and returns the chain of NavItems leading to (and including) the item matching the given pathname, supporting dynamic `{id}` segments.
 */
const findActivePathItems = (items: (NavItem | NavItemGroup)[], pathname: string): NavItem[] => {
  const result: NavItem[] = []

  const findInItems = (items: NavItem[], currentPath: string) => {
    for (const item of items) {
      if (item.url === currentPath || (item.url.includes('{id}') && new RegExp(`^${item.url.replace('{id}', '[^/]+')}$`).test(currentPath))) {
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

/**
 * Returns true when the user may actually open the screen a breadcrumb entry points at: the permissions the route is
 * guarded with in `auth-urls` are granted, and the screen is one they can use with the tenant they are acting in.
 * This is the same pair of checks the sidebar filters its entries with, so a trail never offers a link that would only
 * land on /unauthorized or bounce the user somewhere else.
 */
const canOpen = (item: NavItem, authState: AuthState): boolean => {
  const authUrl = authUrls.find((url) => url.url === item.url)
  if (authUrl?.permissions && !isAllowed(authState, authUrl.permissions)) {
    return false
  }
  return isPathAvailable(authState.user, item.url)
}

/** Props for the Breadcrumbs component, accepting optional extra styling on the list element. */
interface BreadcrumbsProps {
  className?: string
}

/**
 * Breadcrumbs renders the navigation trail for the current path, using a localized link to "/admin" as the home entry and translating each segment's title from the nav-items configuration.
 * An ancestor the user may not open is rendered as plain text, the same way the current page is, rather than as a link.
 * It is a client component that reads the current pathname with usePathname.
 */
export function Breadcrumbs({ className }: BreadcrumbsProps) {
  const pathname = usePathname()
  // get the path items based on pathname, support dynamic route like /admin/items/{id}
  const normalizedPathname = pathname.replace(/^\/[a-z]{2}(\/|$)/, '/')
  const activePathItems = findActivePathItems(navItems, normalizedPathname)
  const { t } = useTranslation()
  const authState = useAppSelector((state) => state.auth)

  return (
    <ol className={cn('flex min-w-0 items-center gap-1.5 text-[13px] text-muted-foreground', className)}>
      <li className="shrink-0">
        <Link href="/admin" className="truncate transition-colors hover:text-foreground">
          {t('navigation.home')}
        </Link>
      </li>
      {activePathItems.map((item, index) => (
        <li key={item.url} className="flex min-w-0 items-center gap-1.5">
          <ChevronRight size={14} className="shrink-0 text-subtle-foreground rtl:rotate-180" />
          {index === activePathItems.length - 1 || !canOpen(item, authState) ? (
            <span className={cn('truncate', index === activePathItems.length - 1 && 'font-medium text-foreground')}>{t(item.title)}</span>
          ) : (
            /* eslint-disable-next-line @typescript-eslint/no-explicit-any */
            <Link href={item.url as any} className="truncate transition-colors hover:text-foreground">
              {t(item.title)}
            </Link>
          )}
        </li>
      ))}
    </ol>
  )
}
