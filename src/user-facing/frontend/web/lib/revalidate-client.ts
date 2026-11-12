import { localeFromPathname, withLocale } from '@/i18n/locale-path'
import { adminPath } from '@/lib/paths'

/** The cache tags an admin screen may clear: the public pages' metadata, and the anonymous dictionary. */
export type PublicCacheTag = 'seo' | 'localization'

/**
 * Asks the web server to drop its cached copies of what an administrator just changed, so the public
 * pages show the edit on their next request rather than when the cache runs out. Posts to the admin
 * area's revalidate route (`/admin/revalidate`), which checks the caller's own permissions. Best effort:
 * the save already succeeded, and a failed revalidation only means the old copy lives out its five minutes.
 */
export const revalidatePublicCache = async (tags: PublicCacheTag[]): Promise<boolean> => {
  if (typeof window === 'undefined') return false
  try {
    const response = await fetch(withLocale(adminPath('/revalidate'), localeFromPathname(window.location.pathname)), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'same-origin',
      body: JSON.stringify({ tags }),
    })
    return response.ok
  } catch {
    return false
  }
}
