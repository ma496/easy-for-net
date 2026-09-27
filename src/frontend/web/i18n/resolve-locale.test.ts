import { describe, expect, it } from 'vitest'
import { enabledLocales, resolveLocale } from './resolve-locale'

const locales = ['en', 'ar', 'fr'] as const

describe('resolveLocale', () => {
  it("prefers the visitor's own preferred-language cookie over everything else", () => {
    expect(resolveLocale(locales, 'en', 'ar', 'fr', undefined, 'fr')).toBe('ar')
  })

  it('falls back to the scope-language cookie when there is no usable preference', () => {
    expect(resolveLocale(locales, 'en', undefined, 'fr', undefined, 'ar')).toBe('fr')
  })

  it('ignores a preferred-language cookie naming a locale this deployment does not route', () => {
    expect(resolveLocale(locales, 'en', 'de', 'fr', undefined, 'ar')).toBe('fr')
  })

  it('falls back to the negotiated Accept-Language locale when neither cookie applies', () => {
    expect(resolveLocale(locales, 'en', undefined, undefined, undefined, 'ar')).toBe('ar')
  })

  it('falls back to the project default when nothing else names a routable locale', () => {
    expect(resolveLocale(locales, 'en', undefined, 'de', undefined, 'zh')).toBe('en')
  })

  it('falls back to the project default when nothing is set at all', () => {
    expect(resolveLocale(locales, 'en', undefined, undefined, undefined, undefined)).toBe('en')
  })

  it('ignores a preferred-language cookie naming a locale the scope-languages cookie has disabled', () => {
    expect(resolveLocale(locales, 'en', 'fr', undefined, 'en,ar', 'ar')).toBe('ar')
  })

  it('ignores a scope-language default the scope-languages cookie has disabled', () => {
    expect(resolveLocale(locales, 'en', undefined, 'fr', 'en,ar', undefined)).toBe('en')
  })

  it('falls back to the first enabled locale when the deployment default is itself disabled', () => {
    expect(resolveLocale(locales, 'en', undefined, undefined, 'ar,fr', undefined)).toBe('ar')
  })

  it('treats an empty scope-languages cookie as no restriction at all', () => {
    expect(resolveLocale(locales, 'en', undefined, 'fr', '', undefined)).toBe('fr')
  })

  it('treats a scope-languages cookie naming nothing routable as no restriction at all', () => {
    expect(resolveLocale(locales, 'en', undefined, 'fr', 'de,it', undefined)).toBe('fr')
  })
})

describe('enabledLocales', () => {
  it('is every routable locale when the cookie is absent', () => {
    expect(enabledLocales(locales, undefined)).toEqual([...locales])
  })

  it("intersects the cookie with the routable set, in the cookie's (the API's) order", () => {
    expect(enabledLocales(locales, 'fr,de,ar,fr')).toEqual(['fr', 'ar'])
  })

  it('falls back to the first enabled locale in the API order, not the routable order', () => {
    expect(resolveLocale(locales, 'en', undefined, undefined, 'fr,ar', undefined)).toBe('fr')
  })

  it('falls back to every routable locale when the intersection is empty', () => {
    expect(enabledLocales(locales, 'de,it')).toEqual([...locales])
  })
})
