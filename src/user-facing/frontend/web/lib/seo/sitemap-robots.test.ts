import { describe, expect, it } from 'vitest'
import { toRobotsTxt } from './robots'
import { toSitemap } from './sitemap'

describe('toSitemap', () => {
  const response = {
    siteUrl: 'https://www.example.com/',
    defaultCulture: 'en',
    cultures: ['en', 'fr', 'xx'],
    entries: [
      { path: '/', lastModified: '2026-11-01T00:00:00Z', changeFrequency: 'weekly' as const, priority: 1 },
      { path: '/about', lastModified: '2026-10-01T00:00:00Z', changeFrequency: null, priority: null },
    ],
  }

  it('lists every page in every routable culture, each with all its alternates', () => {
    const sitemap = toSitemap(response, ['en', 'fr', 'ar'], 'en')

    expect(sitemap.map((entry) => entry.url)).toEqual(['https://www.example.com/', 'https://www.example.com/fr', 'https://www.example.com/about', 'https://www.example.com/fr/about'])
    expect(sitemap[3].alternates).toEqual({ languages: { en: 'https://www.example.com/about', fr: 'https://www.example.com/fr/about' } })
  })

  it('carries the frequency and priority only when the page states them', () => {
    const [home, , about] = toSitemap(response, ['en', 'fr'], 'en')

    expect(home).toMatchObject({ lastModified: '2026-11-01T00:00:00Z', changeFrequency: 'weekly', priority: 1 })
    expect(about).not.toHaveProperty('changeFrequency')
    expect(about).not.toHaveProperty('priority')
  })

  it('falls back to the default culture when none of the API cultures is routable', () => {
    expect(toSitemap({ ...response, cultures: ['xx'] }, ['en'], 'en').map((entry) => entry.url)).toEqual(['https://www.example.com/', 'https://www.example.com/about'])
  })
})

describe('toRobotsTxt', () => {
  it('allows the site, keeps the private areas out and names the sitemap', () => {
    expect(toRobotsTxt({ siteUrl: 'https://www.example.com', allowIndexing: true, extra: '' })).toBe(
      ['User-agent: *', 'Allow: /', 'Disallow: /admin', 'Disallow: /account', 'Disallow: /api', '', 'Sitemap: https://www.example.com/sitemap.xml', ''].join('\n'),
    )
  })

  it('disallows everything when indexing is off', () => {
    const text = toRobotsTxt({ siteUrl: 'https://www.example.com', allowIndexing: false, extra: '' })

    expect(text).toContain('Disallow: /\n')
    expect(text).not.toContain('Allow: /')
  })

  it('appends the extra lines as written', () => {
    const text = toRobotsTxt({ siteUrl: 'https://www.example.com/', allowIndexing: true, extra: 'User-agent: GPTBot\r\nDisallow: /\n' })

    expect(text.endsWith('\n\nUser-agent: GPTBot\nDisallow: /\n')).toBe(true)
    expect(text).toContain('Sitemap: https://www.example.com/sitemap.xml')
  })

  it('disallows everything when the API cannot be asked', () => {
    expect(toRobotsTxt(null)).toBe('User-agent: *\nDisallow: /\n')
  })
})
