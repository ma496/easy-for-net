import { describe, expect, it } from 'vitest'
import { decideLocaleGuardTarget } from './locale-guard'

const routableLocales = ['en', 'fr', 'de'] as const

describe('decideLocaleGuardTarget', () => {
  it('does nothing when the served culture matches the URL and the scope names no default', () => {
    expect(
      decideLocaleGuardTarget({ urlLocale: 'en', servedCulture: 'en', defaultCulture: null, enabledCultures: ['en'], routableLocales, hasPreferredLanguageCookie: false }),
    ).toBeNull()
  })

  it('navigates to the served culture when it differs from the URL - the requested one was not usable', () => {
    expect(
      decideLocaleGuardTarget({ urlLocale: 'de', servedCulture: 'en', defaultCulture: null, enabledCultures: ['en'], routableLocales, hasPreferredLanguageCookie: false }),
    ).toBe('en')
  })

  it('navigates to the scope default for a visitor with no standing preference', () => {
    expect(
      decideLocaleGuardTarget({ urlLocale: 'en', servedCulture: 'en', defaultCulture: 'fr', enabledCultures: ['en', 'fr'], routableLocales, hasPreferredLanguageCookie: false }),
    ).toBe('fr')
  })

  it('leaves a visitor with an explicit preferred-language cookie on the URL locale', () => {
    expect(
      decideLocaleGuardTarget({ urlLocale: 'en', servedCulture: 'en', defaultCulture: 'fr', enabledCultures: ['en', 'fr'], routableLocales, hasPreferredLanguageCookie: true }),
    ).toBeNull()
  })

  it('does nothing once the URL already matches the scope default', () => {
    expect(
      decideLocaleGuardTarget({ urlLocale: 'fr', servedCulture: 'fr', defaultCulture: 'fr', enabledCultures: ['fr'], routableLocales, hasPreferredLanguageCookie: false }),
    ).toBeNull()
  })

  it('never targets the served culture when it is not one of the scope-enabled codes - a defensive check the API contract should already guarantee', () => {
    expect(
      decideLocaleGuardTarget({ urlLocale: 'de', servedCulture: 'fr', defaultCulture: null, enabledCultures: ['en'], routableLocales, hasPreferredLanguageCookie: false }),
    ).toBeNull()
  })

  it('never targets a default culture this deployment does not route', () => {
    expect(
      decideLocaleGuardTarget({ urlLocale: 'en', servedCulture: 'en', defaultCulture: 'it', enabledCultures: ['en', 'it'], routableLocales, hasPreferredLanguageCookie: false }),
    ).toBeNull()
  })

  it('never targets a default culture the scope has not actually enabled', () => {
    expect(
      decideLocaleGuardTarget({ urlLocale: 'en', servedCulture: 'en', defaultCulture: 'fr', enabledCultures: ['en'], routableLocales, hasPreferredLanguageCookie: false }),
    ).toBeNull()
  })
})
