import { describe, expect, it } from 'vitest'
import type { EmailSettingsDto, SettingSource, GeneralSettingsDto, SeoSettingsDto } from '@/store/api/settings'
import {
  buildEmailUpdateValues,
  buildGeneralUpdateValues,
  buildSeoUpdateValues,
  emailFormValues,
  emailSources,
  hasOwnOverride,
  isValidPort,
  isValidSiteUrl,
  isValidTitleTemplate,
  markerOf,
  generalFormValues,
  generalSources,
  seoFormValues,
} from './settings-form'

const emailSettings = (sources: Partial<Record<keyof EmailSettingsDto, SettingSource>> = {}, isPasswordSet = true): EmailSettingsDto => ({
  smtpServer: { value: 'smtp.example.com', source: sources.smtpServer ?? 'default' },
  smtpPort: { value: 587, source: sources.smtpPort ?? 'default' },
  smtpUsername: { value: 'mailer', source: sources.smtpUsername ?? 'default' },
  smtpPassword: { isSet: isPasswordSet, source: sources.smtpPassword ?? 'default' },
  senderEmail: { value: 'no-reply@example.com', source: sources.senderEmail ?? 'default' },
  senderName: { value: 'Example', source: sources.senderName ?? 'default' },
})

const generalSettings = (source: SettingSource, value = false, signup: { value: boolean; source: SettingSource } = { value: false, source: 'default' }): GeneralSettingsDto => ({
  isEmailVerificationRequired: { value, source },
  isSignupEnabled: signup,
})

const seoSettings = (sources: Partial<Record<keyof SeoSettingsDto, SettingSource>> = {}): SeoSettingsDto => ({
  siteName: { value: 'Example', source: sources.siteName ?? 'default' },
  titleTemplate: { value: '%s | Example', source: sources.titleTemplate ?? 'default' },
  defaultDescription: { value: '', source: sources.defaultDescription ?? 'default' },
  defaultOgImageFileId: { value: '', source: sources.defaultOgImageFileId ?? 'default' },
  twitterSite: { value: '', source: sources.twitterSite ?? 'default' },
  siteUrl: { value: 'https://www.example.com', source: sources.siteUrl ?? 'default' },
  allowIndexing: { value: true, source: sources.allowIndexing ?? 'default' },
  robotsExtra: { value: '', source: sources.robotsExtra ?? 'default' },
})

describe('markerOf', () => {
  it('reads the site override as set here and the default as the default', () => {
    expect(markerOf('override')).toBe('own')
    expect(markerOf('default')).toBe('default')
  })
})

describe('hasOwnOverride', () => {
  it('is true only when a property is set here', () => {
    expect(hasOwnOverride(emailSources(emailSettings({ smtpPassword: 'override' })))).toBe(true)
    expect(hasOwnOverride(emailSources(emailSettings()))).toBe(false)
  })
})

describe('isValidPort', () => {
  it.each(['1', '587', '65535', ' 25 ', 2525])('accepts %s', (value) => {
    expect(isValidPort(value)).toBe(true)
  })

  it.each(['', '0', '65536', '25.5', '-1', 'abc', '1e3', 0, 70000, 25.5, null, undefined])('refuses %s', (value) => {
    expect(isValidPort(value)).toBe(false)
  })
})

describe('buildGeneralUpdateValues', () => {
  it('sends nothing when every flag is the default and untouched', () => {
    const settings = generalSettings('default')
    const values = generalFormValues(settings)

    expect(buildGeneralUpdateValues(settings, values, values)).toEqual({})
  })

  it('keeps an own override even when untouched, so saving does not drop it', () => {
    const settings = generalSettings('override', true)
    const values = generalFormValues(settings)

    expect(buildGeneralUpdateValues(settings, values, values)).toEqual({ isEmailVerificationRequired: true })
  })

  it('sends a changed flag', () => {
    const settings = generalSettings('default')
    const initial = generalFormValues(settings)

    expect(buildGeneralUpdateValues(settings, initial, { ...initial, isSignupEnabled: true })).toEqual({ isSignupEnabled: true })
  })
})

describe('generalSources', () => {
  it('counts both flags', () => {
    const settings = generalSettings('default', false, { value: true, source: 'override' })

    expect(generalSources(settings)).toEqual(['default', 'override'])
    expect(hasOwnOverride(generalSources(settings))).toBe(true)
  })
})

describe('buildEmailUpdateValues', () => {
  it('omits an untouched password, so the stored one is kept rather than sent as empty or null', () => {
    const settings = emailSettings({ smtpServer: 'override', smtpPassword: 'override' })
    const initial = emailFormValues(settings)
    const values = buildEmailUpdateValues(settings, initial, initial)

    expect(values).not.toHaveProperty('smtpPassword')
    expect(Object.values(values)).not.toContain(null)
  })

  it('sends the password when one was typed', () => {
    const settings = emailSettings()
    const initial = emailFormValues(settings)

    expect(buildEmailUpdateValues(settings, initial, { ...initial, smtpPassword: 's3cret' })).toEqual({ smtpPassword: 's3cret' })
  })

  it('sends an empty password when Clear was pressed, whatever the field holds', () => {
    const settings = emailSettings({ smtpPassword: 'override' })
    const initial = emailFormValues(settings)

    expect(buildEmailUpdateValues(settings, initial, { ...initial, smtpPassword: 'ignored', smtpPasswordCleared: true })).toEqual({ smtpPassword: '' })
  })

  it('sends own overrides and changed properties, and leaves untouched defaults out', () => {
    const settings = emailSettings({ smtpServer: 'override', senderName: 'default' })
    const initial = emailFormValues(settings)

    expect(buildEmailUpdateValues(settings, initial, { ...initial, senderName: 'Changed' })).toEqual({ smtpServer: 'smtp.example.com', senderName: 'Changed' })
  })

  it('sends the port as a number, read whichever shape the input handed over', () => {
    const settings = emailSettings()
    const initial = emailFormValues(settings)

    expect(buildEmailUpdateValues(settings, initial, { ...initial, smtpPort: ' 2525 ' })).toEqual({ smtpPort: 2525 })
    expect(buildEmailUpdateValues(settings, initial, { ...initial, smtpPort: 2525 })).toEqual({ smtpPort: 2525 })
    expect(buildEmailUpdateValues(settings, initial, { ...initial, smtpPort: 587 })).toEqual({})
  })

  it('starts the password field empty and uncleared whatever is stored', () => {
    const values = emailFormValues(emailSettings({ smtpPassword: 'override' }, true))

    expect(values.smtpPassword).toBe('')
    expect(values.smtpPasswordCleared).toBe(false)
    expect(values.smtpPort).toBe('587')
  })
})

describe('isValidTitleTemplate', () => {
  it('needs the %s placeholder', () => {
    expect(isValidTitleTemplate('%s | Example')).toBe(true)
    expect(isValidTitleTemplate('Example')).toBe(false)
    expect(isValidTitleTemplate('')).toBe(false)
  })
})

describe('isValidSiteUrl', () => {
  it.each(['', 'https://www.example.com', 'http://localhost:3000', 'https://example.com/'])('accepts %j', (value) => {
    expect(isValidSiteUrl(value)).toBe(true)
  })

  it.each(['example.com', 'ftp://example.com', 'https://example.com/blog', 'https://example.com?x=1'])('refuses %j', (value) => {
    expect(isValidSiteUrl(value)).toBe(false)
  })
})

describe('buildSeoUpdateValues', () => {
  it('sends what changed and what is already set here, and nothing else', () => {
    const settings = seoSettings({ siteName: 'override' })
    const initial = seoFormValues(settings)

    expect(buildSeoUpdateValues(settings, initial, { ...initial, allowIndexing: false })).toEqual({ siteName: 'Example', allowIndexing: false })
  })

  it('drops a trailing slash from the site address', () => {
    const settings = seoSettings()
    const initial = seoFormValues(settings)

    expect(buildSeoUpdateValues(settings, initial, { ...initial, siteUrl: ' https://shop.example.com/ ' })).toEqual({ siteUrl: 'https://shop.example.com' })
  })

  it('sends nothing for an untouched form of defaults', () => {
    const settings = seoSettings()
    const initial = seoFormValues(settings)

    expect(buildSeoUpdateValues(settings, initial, initial)).toEqual({})
  })
})
