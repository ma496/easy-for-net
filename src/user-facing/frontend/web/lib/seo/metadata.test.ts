import { describe, expect, it } from 'vitest'
import { absoluteUrl, fallbackMetadata, localizedPath, pageTitle, safeJsonLd, toMetadata, type MetadataContext } from './metadata'
import type { SeoResolveResponse } from './seo-dtos'

const resolved = (overrides: Partial<SeoResolveResponse> = {}): SeoResolveResponse => ({
  title: 'About us',
  titleTemplate: '%s | Example',
  siteName: 'Example',
  siteUrl: 'https://www.example.com',
  description: 'Who we are.',
  keywords: 'team, story ,, values',
  canonicalUrl: null,
  ogTitle: null,
  ogDescription: null,
  ogImageUrl: 'https://www.example.com/api/files/og.png',
  twitterCard: 'summary_large_image',
  twitterSite: '@example',
  index: true,
  follow: true,
  jsonLd: null,
  ...overrides,
})

const context = (overrides: Partial<MetadataContext> = {}): MetadataContext => ({ lang: 'en', path: '/about', cultures: ['en', 'fr', 'ar'], defaultCulture: 'en', ...overrides })

describe('localizedPath', () => {
  it('gives the default culture no prefix and every other culture its own', () => {
    expect(localizedPath('/about', 'en', 'en')).toBe('/about')
    expect(localizedPath('/about', 'fr', 'en')).toBe('/fr/about')
    expect(localizedPath('/', 'en', 'en')).toBe('/')
    expect(localizedPath('/', 'fr', 'en')).toBe('/fr')
  })
})

describe('absoluteUrl', () => {
  it('joins without doubling the slash', () => {
    expect(absoluteUrl('https://www.example.com/', '/about')).toBe('https://www.example.com/about')
  })
})

describe('pageTitle', () => {
  it('puts the page title through the template', () => {
    expect(pageTitle(resolved())).toBe('About us | Example')
  })

  it('falls back to the site name when the page has no title', () => {
    expect(pageTitle(resolved({ title: null }))).toBe('Example')
    expect(pageTitle(resolved({ title: '   ' }))).toBe('Example')
  })

  it('uses the title alone when the template has no placeholder', () => {
    expect(pageTitle(resolved({ titleTemplate: 'Example' }))).toBe('About us')
  })
})

describe('toMetadata', () => {
  it('sets an absolute title, so the root layout template does not apply twice', () => {
    expect(toMetadata(resolved(), context()).title).toEqual({ absolute: 'About us | Example' })
  })

  it('builds the canonical address and hreflang alternates for the default culture', () => {
    const metadata = toMetadata(resolved(), context())

    expect(metadata.alternates?.canonical).toBe('https://www.example.com/about')
    expect(metadata.alternates?.languages).toEqual({
      en: 'https://www.example.com/about',
      fr: 'https://www.example.com/fr/about',
      ar: 'https://www.example.com/ar/about',
      'x-default': 'https://www.example.com/about',
    })
  })

  it('points the canonical address of another culture at its own prefixed page', () => {
    const metadata = toMetadata(resolved(), context({ lang: 'fr' }))

    expect(metadata.alternates?.canonical).toBe('https://www.example.com/fr/about')
    expect(metadata.openGraph).toMatchObject({ url: 'https://www.example.com/fr/about', locale: 'fr' })
  })

  it('names the home page of another culture without a trailing slash', () => {
    expect(toMetadata(resolved(), context({ lang: 'fr', path: '/' })).alternates?.canonical).toBe('https://www.example.com/fr')
  })

  it('keeps an explicit canonical address as given', () => {
    expect(toMetadata(resolved({ canonicalUrl: 'https://other.example.com/about' }), context()).alternates?.canonical).toBe('https://other.example.com/about')
  })

  it('lists the page culture among the alternates even when the served list lacks it', () => {
    expect(Object.keys(toMetadata(resolved(), context({ lang: 'ru', cultures: ['en'] })).alternates?.languages ?? {})).toEqual(['en', 'ru', 'x-default'])
  })

  it('maps the robots directives', () => {
    expect(toMetadata(resolved({ index: false, follow: true }), context()).robots).toEqual({ index: false, follow: true })
  })

  it('falls back from the sharing fields to the page title and description', () => {
    const metadata = toMetadata(resolved(), context())

    expect(metadata.openGraph).toMatchObject({ type: 'website', title: 'About us | Example', description: 'Who we are.', siteName: 'Example', images: ['https://www.example.com/api/files/og.png'] })
    expect(metadata.twitter).toMatchObject({ card: 'summary_large_image', site: '@example', title: 'About us | Example', description: 'Who we are.' })
  })

  it('prefers the sharing fields when they are set', () => {
    const metadata = toMetadata(resolved({ ogTitle: 'Meet the team', ogDescription: 'Say hello.' }), context())

    expect(metadata.openGraph).toMatchObject({ title: 'Meet the team', description: 'Say hello.' })
  })

  it('splits keywords and drops blanks, and sets the metadata base', () => {
    const metadata = toMetadata(resolved(), context())

    expect(metadata.keywords).toEqual(['team', 'story', 'values'])
    expect(metadata.metadataBase?.toString()).toBe('https://www.example.com/')
  })

  it('leaves out what the page does not have', () => {
    const metadata = toMetadata(resolved({ description: null, keywords: null, ogImageUrl: null, twitterSite: null }), context())

    expect(metadata.description).toBeUndefined()
    expect(metadata.keywords).toBeUndefined()
    expect(metadata.openGraph?.images).toBeUndefined()
    expect(metadata.twitter).toMatchObject({ site: undefined })
  })
})

describe('fallbackMetadata', () => {
  it('titles the page with the brand when the API cannot be asked', () => {
    expect(fallbackMetadata('Easy For Net')).toEqual({ title: { absolute: 'Easy For Net' } })
  })
})

describe('safeJsonLd', () => {
  it('re-serializes a JSON document and escapes anything that could close the script', () => {
    expect(safeJsonLd('{ "name": "</script><script>alert(1)</script>" }')).toBe('{"name":"\\u003c/script>\\u003cscript>alert(1)\\u003c/script>"}')
  })

  it('renders nothing for no document or text that is not JSON', () => {
    expect(safeJsonLd(null)).toBeNull()
    expect(safeJsonLd('  ')).toBeNull()
    expect(safeJsonLd('{ nope')).toBeNull()
  })
})
