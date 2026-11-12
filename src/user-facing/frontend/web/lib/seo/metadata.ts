import type { Metadata } from 'next'
import type { SeoResolveResponse } from './seo-dtos'

/**
 * The address of `path` in `culture`: the routing default culture carries no prefix (`/about`), every
 * other culture its own (`/fr/about`, and `/fr` for the home page).
 */
export const localizedPath = (path: string, culture: string, defaultCulture: string): string => {
  const normalized = path.startsWith('/') ? path : `/${path}`
  if (culture === defaultCulture) return normalized
  return normalized === '/' ? `/${culture}` : `/${culture}${normalized}`
}

/** An absolute address on the site, with no doubled slash and no trailing slash on the origin. */
export const absoluteUrl = (siteUrl: string, path: string): string => `${siteUrl.replace(/\/+$/, '')}${path.startsWith('/') ? path : `/${path}`}`

/** The page's full title: its own title through the template, or the site name when it has none. */
export const pageTitle = (resolved: Pick<SeoResolveResponse, 'title' | 'titleTemplate' | 'siteName'>): string => {
  const title = resolved.title?.trim()
  if (!title) return resolved.siteName
  return resolved.titleTemplate?.includes('%s') ? resolved.titleTemplate.replace('%s', title) : title
}

/** Where the page is and which languages it is served in. */
export interface MetadataContext {
  /** The culture the page is rendered in. */
  lang: string
  /** The locale-free path, `/` for the home page. */
  path: string
  /** The cultures the site serves, for the hreflang alternates. */
  cultures: readonly string[]
  /** The culture whose addresses carry no prefix. */
  defaultCulture: string
}

/** A `URL` for the site's origin, or undefined when the API sent something that is not one. */
const toUrl = (value: string): URL | undefined => {
  try {
    return new URL(value)
  } catch {
    return undefined
  }
}

/** The keywords as a list, split on commas, blanks dropped. */
const keywordsOf = (keywords: string | null): string[] | undefined => {
  const list = (keywords ?? '')
    .split(',')
    .map((keyword) => keyword.trim())
    .filter(Boolean)
  return list.length > 0 ? list : undefined
}

/**
 * Maps what `GET /seo/resolve` answered onto Next's `Metadata`: the title (absolute, so the root
 * layout's template does not apply twice), description and keywords, the canonical address and one
 * hreflang alternate per served culture plus `x-default`, Open Graph and X (Twitter) cards, and the
 * robots directives. Pure, so every rule is testable without a request.
 */
export const toMetadata = (resolved: SeoResolveResponse, context: MetadataContext): Metadata => {
  const siteUrl = resolved.siteUrl.replace(/\/+$/, '')
  const title = pageTitle(resolved)
  const description = resolved.description?.trim() || undefined
  const url = absoluteUrl(siteUrl, localizedPath(context.path, context.lang, context.defaultCulture))
  const canonical = resolved.canonicalUrl?.trim() || url
  const cultures = context.cultures.includes(context.lang) ? context.cultures : [...context.cultures, context.lang]
  const languages: Record<string, string> = Object.fromEntries(cultures.map((culture) => [culture, absoluteUrl(siteUrl, localizedPath(context.path, culture, context.defaultCulture))]))
  languages['x-default'] = absoluteUrl(siteUrl, localizedPath(context.path, context.defaultCulture, context.defaultCulture))

  const socialTitle = resolved.ogTitle?.trim() || title
  const socialDescription = resolved.ogDescription?.trim() || description
  const images = resolved.ogImageUrl ? [resolved.ogImageUrl] : undefined

  return {
    title: { absolute: title },
    description,
    keywords: keywordsOf(resolved.keywords),
    metadataBase: toUrl(siteUrl),
    alternates: { canonical, languages },
    openGraph: {
      type: 'website',
      title: socialTitle,
      description: socialDescription,
      siteName: resolved.siteName,
      url: canonical,
      locale: context.lang,
      images,
    },
    twitter: {
      card: resolved.twitterCard,
      site: resolved.twitterSite?.trim() || undefined,
      title: socialTitle,
      description: socialDescription,
      images,
    },
    robots: { index: resolved.index, follow: resolved.follow },
  }
}

/** What a public page says about itself when the API cannot be asked: the brand as its title, and nothing it cannot vouch for. */
export const fallbackMetadata = (brandName: string): Metadata => ({
  title: { absolute: brandName },
})

/**
 * The JSON-LD document ready to sit inside a `<script>`: re-serialized so it is known to be JSON, with
 * every `<` escaped so the text can never close the element. Null when there is none, or it is not JSON.
 */
export const safeJsonLd = (jsonLd: string | null | undefined): string | null => {
  if (!jsonLd?.trim()) return null
  try {
    return JSON.stringify(JSON.parse(jsonLd)).replace(/</g, '\\u003c')
  } catch {
    return null
  }
}
