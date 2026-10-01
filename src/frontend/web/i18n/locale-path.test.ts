import { describe, expect, it } from 'vitest'
import { i18nConfig } from './config'
import { localeFromPathname, withLocale } from './locale-path'

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
    expect(withLocale('/admin/users', defaultLocale)).toBe('/admin/users')
  })

  it.skipIf(!localeA)('adds the locale segment to an unprefixed path', () => {
    expect(withLocale('/admin/users', localeA)).toBe(`/${localeA}/admin/users`)
  })

  it.skipIf(!(localeA && localeB))('swaps an existing locale segment for another one', () => {
    expect(withLocale(`/${localeA}/admin/users`, localeB)).toBe(`/${localeB}/admin/users`)
  })

  it.skipIf(!localeA)('drops the locale segment when switching to the default locale', () => {
    expect(withLocale(`/${localeA}/admin/users`, defaultLocale)).toBe('/admin/users')
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

describe('localeFromPathname', () => {
  it('falls back to the default locale for an unprefixed path', () => {
    expect(localeFromPathname('/admin/users')).toBe(defaultLocale)
  })

  it('falls back to the default locale for the root path', () => {
    expect(localeFromPathname('/')).toBe(defaultLocale)
  })

  it.skipIf(!localeA)('reads the locale segment off a prefixed path', () => {
    expect(localeFromPathname(`/${localeA}/admin/users`)).toBe(localeA)
  })

  it('falls back to the default locale when the first segment names no routable locale', () => {
    expect(localeFromPathname('/not-a-locale/admin/users')).toBe(defaultLocale)
  })
})
