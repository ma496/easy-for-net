/** How a page's card looks when shared on X (Twitter). */
export type TwitterCard = 'summary' | 'summary_large_image'

/** How often a page is expected to change, as the sitemap protocol spells it. */
export type ChangeFrequency = 'always' | 'hourly' | 'daily' | 'weekly' | 'monthly' | 'yearly' | 'never'

/**
 * Response of anonymous `GET /seo/resolve?path=&culture=`: everything a public page's metadata is built
 * from, the page's own SEO record merged over the site-wide `Seo` setting by the API.
 */
export interface SeoResolveResponse {
  title: string | null
  /** The title pattern, `%s` standing for the page title. */
  titleTemplate: string
  siteName: string
  /** The site's public origin, absolute, with no trailing slash. */
  siteUrl: string
  description: string | null
  keywords: string | null
  canonicalUrl: string | null
  ogTitle: string | null
  ogDescription: string | null
  /** Absolute. */
  ogImageUrl: string | null
  twitterCard: TwitterCard
  twitterSite: string | null
  index: boolean
  follow: boolean
  /** A JSON-LD document, as text. */
  jsonLd: string | null
}

/** One page of the sitemap, before it is multiplied by culture. */
export interface SeoSitemapEntry {
  path: string
  lastModified: string
  changeFrequency: ChangeFrequency | null
  priority: number | null
}

/** Response of anonymous `GET /seo/sitemap`. */
export interface SeoSitemapResponse {
  siteUrl: string
  defaultCulture: string
  cultures: string[]
  entries: SeoSitemapEntry[]
}

/** Response of anonymous `GET /seo/robots`. */
export interface SeoRobotsResponse {
  siteUrl: string
  allowIndexing: boolean
  /** Extra `robots.txt` lines, one per line. */
  extra: string
}
