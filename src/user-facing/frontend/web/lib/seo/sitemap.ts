import type { MetadataRoute } from 'next'
import { absoluteUrl, localizedPath } from './metadata'
import type { SeoSitemapResponse } from './seo-dtos'

/**
 * Maps `GET /seo/sitemap` onto the sitemap Next serves: one entry per page per culture, each listing
 * every culture's address of the same page as its alternates. Only cultures this app routes to are
 * listed; the routing default culture carries no prefix.
 */
export const toSitemap = (response: SeoSitemapResponse, routableCultures: readonly string[], defaultCulture: string): MetadataRoute.Sitemap => {
  const cultures = response.cultures.filter((culture) => routableCultures.includes(culture))
  const served = cultures.length > 0 ? cultures : [defaultCulture]

  return response.entries.flatMap((entry) => {
    const languages = Object.fromEntries(served.map((culture) => [culture, absoluteUrl(response.siteUrl, localizedPath(entry.path, culture, defaultCulture))]))
    return served.map((culture) => ({
      url: languages[culture],
      lastModified: entry.lastModified,
      ...(entry.changeFrequency ? { changeFrequency: entry.changeFrequency } : {}),
      ...(entry.priority !== null && entry.priority !== undefined ? { priority: entry.priority } : {}),
      alternates: { languages },
    }))
  })
}
