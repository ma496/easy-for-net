import type { MetadataRoute } from 'next'
import { i18nConfig } from '@/i18n/config'
import { fetchSitemap } from '@/lib/server/seo'
import { toSitemap } from '@/lib/seo/sitemap'

/** Rendered on request, never at build time: the pages and their dates are the API's, edited at run time. */
export const dynamic = 'force-dynamic'

/** `/sitemap.xml`, built from `GET /seo/sitemap`; empty when the API cannot be asked. */
export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const response = await fetchSitemap()
  return response ? toSitemap(response, i18nConfig.locales, i18nConfig.defaultLocale) : []
}
