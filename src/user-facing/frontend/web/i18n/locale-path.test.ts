import { describe, expect, it } from 'vitest'
import { i18nConfig } from './config'
import { localeFromPathname, withLocale, withoutLocale } from './locale-path'

// Two of this deployment's actual non-default locales, so the test exercises `withLocale` against the
// same `i18nConfig` it reads internally rather than codes that may not be routable at all (a `-m false`
// project ships only `en`, in which case there is nothing to swap between or to prefix with, and the
// cases that need a second locale are skipped rather than asserting against a locale that does not exist).
const defaultLocale = i18nConfig.defaultLocale
const [localeA, localeB] = i18nConfig.locales.filter((locale) => locale !== defaultLocale)

describe('withLocale', () => {
  it('adds the locale segment to the root path', () => {
    expect(withLocale('/', defaultLocale)).toBe('/')
  })

  it('leaves an unprefixed path unprefixed when switching to the default locale', () => {
    expect(withLocale('/users', defaultLocale)).toBe('/users')
  })

  it.skipIf(!localeA)('adds the locale segment to an unprefixed path', () => {
    expect(withLocale('/users', localeA)).toBe(`/${localeA}/users`)
  })

  it.skipIf(!(localeA && localeB))('swaps an existing locale segment for another one', () => {
    expect(withLocale(`/${localeA}/users`, localeB)).toBe(`/${localeB}/users`)
  })

  it.skipIf(!localeA)('drops the locale segment when switching to the default locale', () => {
    expect(withLocale(`/${localeA}/users`, defaultLocale)).toBe('/users')
  })

  it.skipIf(!localeA)('adds the locale segment to the root path for a non-default locale', () => {
    expect(withLocale('/', localeA)).toBe(`/${localeA}`)
  })

  it.skipIf(!localeA)('renders the root path when switching to the default locale', () => {
    expect(withLocale(`/${localeA}`, defaultLocale)).toBe('/')
  })

  it('never returns a protocol-relative path for a pathname with an empty first segment', () => {
    expect(withLocale('//evil.com', defaultLocale)).toBe('/evil.com')
  })
})

describe('withoutLocale', () => {
  it('leaves an unprefixed path as it is', () => {
    expect(withoutLocale('/users')).toBe('/users')
    expect(withoutLocale('/')).toBe('/')
  })

  it('never mistakes a two-letter route for a locale', () => {
    expect(withoutLocale('/ui/buttons')).toBe('/ui/buttons')
  })

  it('removes the default locale segment', () => {
    expect(withoutLocale(`/${defaultLocale}/users`)).toBe('/users')
  })

  it.skipIf(!localeA)('removes a non-default locale segment, leaving the root of a locale as /', () => {
    expect(withoutLocale(`/${localeA}/users`)).toBe('/users')
    expect(withoutLocale(`/${localeA}`)).toBe('/')
  })
})

describe('localeFromPathname', () => {
  it('falls back to the default locale for an unprefixed path', () => {
    expect(localeFromPathname('/users')).toBe(defaultLocale)
  })

  it('falls back to the default locale for the root path', () => {
    expect(localeFromPathname('/')).toBe(defaultLocale)
  })

  it.skipIf(!localeA)('reads the locale segment off a prefixed path', () => {
    expect(localeFromPathname(`/${localeA}/users`)).toBe(localeA)
  })

  it('falls back to the default locale when the first segment names no routable locale', () => {
    expect(localeFromPathname('/not-a-locale/users')).toBe(defaultLocale)
  })
})
