import { describe, expect, it } from 'vitest'
import type { EmailSettingsDto, SettingSource, SigninSettingsDto } from '@/store/api/settings'
import {
  buildEmailUpdateValues,
  buildSigninUpdateValues,
  emailFormValues,
  emailSources,
  hasOwnOverride,
  isValidPort,
  markerOf,
  ownSourceOf,
  signinFormValues,
} from './settings-form'

const emailSettings = (sources: Partial<Record<keyof EmailSettingsDto, SettingSource>> = {}, isPasswordSet = true): EmailSettingsDto => ({
  smtpServer: { value: 'smtp.example.com', source: sources.smtpServer ?? 'default' },
  smtpPort: { value: 587, source: sources.smtpPort ?? 'default' },
  smtpUsername: { value: 'mailer', source: sources.smtpUsername ?? 'default' },
  smtpPassword: { isSet: isPasswordSet, source: sources.smtpPassword ?? 'default' },
  senderEmail: { value: 'no-reply@example.com', source: sources.senderEmail ?? 'default' },
  senderName: { value: 'Example', source: sources.senderName ?? 'default' },
})

const signinSettings = (source: SettingSource, value = false): SigninSettingsDto => ({ isEmailVerificationRequired: { value, source } })

describe('ownSourceOf', () => {
  it('writes to the tenant inside a tenant and to the platform in none', () => {
    expect(ownSourceOf(true)).toBe('tenant')
    expect(ownSourceOf(false)).toBe('platform')
  })
})

describe('markerOf', () => {
  it.each([
    ['tenant', 'tenant', 'own'],
    ['platform', 'tenant', 'platform'],
    ['default', 'tenant', 'default'],
    ['platform', 'platform', 'own'],
    ['default', 'platform', 'default'],
  ] as const)('reads source %s seen from %s as %s', (source, ownSource, marker) => {
    expect(markerOf(source, ownSource)).toBe(marker)
  })
})

describe('hasOwnOverride', () => {
  it('is true only when a property comes from the scope being edited', () => {
    expect(hasOwnOverride(emailSources(emailSettings({ smtpPassword: 'tenant' })), 'tenant')).toBe(true)
    expect(hasOwnOverride(emailSources(emailSettings({ smtpServer: 'platform' })), 'tenant')).toBe(false)
    expect(hasOwnOverride(emailSources(emailSettings({ smtpServer: 'platform' })), 'platform')).toBe(true)
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

describe('buildSigninUpdateValues', () => {
  it('sends nothing when the flag is inherited and untouched', () => {
    const settings = signinSettings('platform')
    const values = signinFormValues(settings)

    expect(buildSigninUpdateValues(settings, values, values, 'tenant')).toEqual({})
  })

  it('keeps an own override even when untouched, so saving does not drop it', () => {
    const settings = signinSettings('tenant', true)
    const values = signinFormValues(settings)

    expect(buildSigninUpdateValues(settings, values, values, 'tenant')).toEqual({ isEmailVerificationRequired: true })
  })

  it('sends a changed inherited flag', () => {
    const settings = signinSettings('default')
    const initial = signinFormValues(settings)

    expect(buildSigninUpdateValues(settings, initial, { isEmailVerificationRequired: true }, 'platform')).toEqual({ isEmailVerificationRequired: true })
  })
})

describe('buildEmailUpdateValues', () => {
  it('omits an untouched password, so the stored one is kept rather than sent as empty or null', () => {
    const settings = emailSettings({ smtpServer: 'tenant', smtpPassword: 'tenant' })
    const initial = emailFormValues(settings)
    const values = buildEmailUpdateValues(settings, initial, initial, 'tenant')

    expect(values).not.toHaveProperty('smtpPassword')
    expect(Object.values(values)).not.toContain(null)
  })

  it('sends the password when one was typed', () => {
    const settings = emailSettings()
    const initial = emailFormValues(settings)

    expect(buildEmailUpdateValues(settings, initial, { ...initial, smtpPassword: 's3cret' }, 'tenant')).toEqual({ smtpPassword: 's3cret' })
  })

  it('sends an empty password when Clear was pressed, whatever the field holds', () => {
    const settings = emailSettings({ smtpPassword: 'tenant' })
    const initial = emailFormValues(settings)

    expect(buildEmailUpdateValues(settings, initial, { ...initial, smtpPassword: 'ignored', smtpPasswordCleared: true }, 'tenant')).toEqual({ smtpPassword: '' })
  })

  it('sends own overrides and changed properties, and leaves untouched inherited ones out', () => {
    const settings = emailSettings({ smtpServer: 'tenant', smtpUsername: 'platform', senderName: 'default' })
    const initial = emailFormValues(settings)
    const current = { ...initial, senderName: 'Changed' }

    expect(buildEmailUpdateValues(settings, initial, current, 'tenant')).toEqual({ smtpServer: 'smtp.example.com', senderName: 'Changed' })
  })

  it('sends the port as a number', () => {
    const settings = emailSettings()
    const initial = emailFormValues(settings)

    expect(buildEmailUpdateValues(settings, initial, { ...initial, smtpPort: ' 2525 ' }, 'platform')).toEqual({ smtpPort: 2525 })
  })

  it('reads a port the number input handed over as a number, and leaves an unchanged one out', () => {
    const settings = emailSettings()
    const initial = emailFormValues(settings)

    expect(buildEmailUpdateValues(settings, initial, { ...initial, smtpPort: 2525 }, 'tenant')).toEqual({ smtpPort: 2525 })
    expect(buildEmailUpdateValues(settings, initial, { ...initial, smtpPort: 587 }, 'tenant')).toEqual({})
  })

  it('treats the platform as the own layer in platform scope', () => {
    const settings = emailSettings({ senderEmail: 'platform' })
    const initial = emailFormValues(settings)

    expect(buildEmailUpdateValues(settings, initial, initial, 'platform')).toEqual({ senderEmail: 'no-reply@example.com' })
    expect(buildEmailUpdateValues(settings, initial, initial, 'tenant')).toEqual({})
  })

  it('starts the password field empty and uncleared whatever is stored', () => {
    const values = emailFormValues(emailSettings({ smtpPassword: 'tenant' }, true))

    expect(values.smtpPassword).toBe('')
    expect(values.smtpPasswordCleared).toBe(false)
    expect(values.smtpPort).toBe('587')
  })
})
