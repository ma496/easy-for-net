import type { SeoRobotsResponse } from './seo-dtos'

/** The paths no crawler is ever invited into, whatever the site allows: the admin area, the account screens and the API. */
export const ALWAYS_DISALLOWED = ['/admin', '/account', '/api']

/**
 * The `robots.txt` text for `GET /seo/robots`: everything allowed when the site allows indexing, else
 * everything disallowed; the admin area, account screens and API always disallowed; the sitemap's
 * address; then the administrator's extra lines verbatim. With no answer from the API, everything is
 * disallowed - a site that cannot say what it wants must not be indexed by accident.
 */
export const toRobotsTxt = (robots: SeoRobotsResponse | null): string => {
  const lines = ['User-agent: *']
  if (robots?.allowIndexing) {
    lines.push('Allow: /')
    lines.push(...ALWAYS_DISALLOWED.map((path) => `Disallow: ${path}`))
  } else {
    lines.push('Disallow: /')
  }

  if (robots?.siteUrl) {
    lines.push('', `Sitemap: ${robots.siteUrl.replace(/\/+$/, '')}/sitemap.xml`)
  }

  const extra = (robots?.extra ?? '').replace(/\r\n/g, '\n').trim()
  if (extra) {
    lines.push('', extra)
  }

  return `${lines.join('\n')}\n`
}
