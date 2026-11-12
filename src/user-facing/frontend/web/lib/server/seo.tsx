import { cache } from 'react'
import type { Metadata } from 'next'
import { getDictionary, i18nConfig, type Locale } from '@/i18n'
import { fallbackMetadata, safeJsonLd, toMetadata } from '@/lib/seo/metadata'
import type { SeoResolveResponse, SeoRobotsResponse, SeoSitemapResponse } from '@/lib/seo/seo-dtos'
import { serverApiFetch } from './api-fetch'
import { mustRethrow } from './dynamic-usage'

/** The cache tag every SEO fetch carries, which the admin screens revalidate after an edit. */
export const SEO_CACHE_TAG = 'seo'

/**
 * Fetches one of the anonymous `/seo` endpoints: public and the same for every visitor, so no cookie
 * and no host is forwarded and the answer is cached for five minutes under the `seo` tag (cleared at
 * once when an administrator saves). Null when the API cannot be reached or answers with an error.
 */
const fetchSeo = async <T,>(path: string): Promise<T | null> => {
  try {
    const response = await serverApiFetch(path, { forwardHost: false, next: { revalidate: 300, tags: [SEO_CACHE_TAG] } })
    if (!response.ok) return null
    return (await response.json()) as T
  } catch (error) {
    if (mustRethrow(error)) throw error
    console.error(`Failed to load ${path}`, error)
    return null
  }
}

/** What the API resolves for one locale-free path in one culture; memoized per request, so the metadata and the JSON-LD share one fetch. */
export const resolveSeo = cache(async (lang: string, path: string): Promise<SeoResolveResponse | null> =>
  fetchSeo<SeoResolveResponse>(`/seo/resolve?path=${encodeURIComponent(path)}&culture=${encodeURIComponent(lang)}`),
)

/** The sitemap's pages, or null when the API cannot be asked. */
export const fetchSitemap = (): Promise<SeoSitemapResponse | null> => fetchSeo<SeoSitemapResponse>('/seo/sitemap')

/** The robots settings, or null when the API cannot be asked. */
export const fetchRobots = (): Promise<SeoRobotsResponse | null> => fetchSeo<SeoRobotsResponse>('/seo/robots')

/** The cultures the site serves and routes to, for the hreflang alternates: the dictionary's enabled languages, else the page's own. */
const servedCultures = async (lang: string): Promise<string[]> => {
  const dictionary = await getDictionary(lang as Locale)
  const routable = i18nConfig.locales as readonly string[]
  const enabled = dictionary.languages.map((language) => language.code).filter((code) => routable.includes(code))
  return enabled.length > 0 ? enabled : [lang]
}

/**
 * The full `Metadata` of a public page - call it from the page's `generateMetadata`:
 * `return buildMetadata(lang, '/about')`. Everything an administrator edits on the SEO screen or the
 * SEO settings tab reaches the page through here, so no title is ever changed in code. When the API
 * cannot be reached the page still renders, titled with the brand.
 */
export const buildMetadata = async (lang: string, path: string): Promise<Metadata> => {
  const resolved = await resolveSeo(lang, path)
  if (!resolved) {
    const dictionary = await getDictionary(lang as Locale)
    return fallbackMetadata(dictionary.resources['brand.name'] || 'Easy For Net')
  }
  return toMetadata(resolved, { lang, path, cultures: await servedCultures(lang), defaultCulture: i18nConfig.defaultLocale })
}

/** Props for {@link JsonLd}: the page it describes. */
interface JsonLdProps {
  lang: string
  path: string
}

/**
 * Server component rendering the page's structured data - the JSON-LD an administrator gave it on the
 * SEO screen - as a `<script type="application/ld+json">`, or nothing when it has none.
 */
export const JsonLd = async ({ lang, path }: JsonLdProps) => {
  const resolved = await resolveSeo(lang, path)
  const json = safeJsonLd(resolved?.jsonLd)
  if (!json) return null
  return <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: json }} />
}
