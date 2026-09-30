import { describe, expect, it } from 'vitest'
import type { SettingDto, SettingPropertyDto } from './settings-dtos'
import { findSetting, toEmailSettings, toSigninSettings } from './settings-mappers'

const property = (overrides: Partial<SettingPropertyDto> & Pick<SettingPropertyDto, 'name'>): SettingPropertyDto => ({
  value: null,
  source: 'default',
  isSecret: false,
  isSet: null,
  ...overrides,
})

const emailSetting = (overrides: Partial<Record<string, Partial<SettingPropertyDto>>> = {}): SettingDto => ({
  name: 'Email',
  properties: [
    property({ name: 'smtpServer', value: 'smtp.example.com', source: 'platform', ...overrides.smtpServer }),
    property({ name: 'smtpPort', value: 587, ...overrides.smtpPort }),
    property({ name: 'smtpUsername', value: 'mailer', source: 'tenant', ...overrides.smtpUsername }),
    property({ name: 'smtpPassword', isSecret: true, isSet: true, source: 'tenant', ...overrides.smtpPassword }),
    property({ name: 'senderEmail', value: 'no-reply@example.com', ...overrides.senderEmail }),
    property({ name: 'senderName', value: 'Example', ...overrides.senderName }),
  ],
})

describe('findSetting', () => {
  it('finds a setting by its registered name and answers undefined for one the API does not declare', () => {
    const items = [emailSetting(), { name: 'Signin', properties: [] }]

    expect(findSetting(items, 'Email')?.name).toBe('Email')
    expect(findSetting([emailSetting()], 'Signin')).toBeUndefined()
    expect(findSetting(undefined, 'Signin')).toBeUndefined()
  })
})

describe('toSigninSettings', () => {
  it('reads the flag and where it came from', () => {
    const dto: SettingDto = { name: 'Signin', properties: [property({ name: 'isEmailVerificationRequired', value: true, source: 'platform' })] }

    expect(toSigninSettings(dto)).toEqual({ isEmailVerificationRequired: { value: true, source: 'platform' } })
  })

  it('answers null when the property is missing, so the card is dropped rather than guessed', () => {
    expect(toSigninSettings({ name: 'Signin', properties: [] })).toBeNull()
  })
})

describe('toEmailSettings', () => {
  it('reads every value with its source and the password as set-or-not only', () => {
    expect(toEmailSettings(emailSetting())).toEqual({
      smtpServer: { value: 'smtp.example.com', source: 'platform' },
      smtpPort: { value: 587, source: 'default' },
      smtpUsername: { value: 'mailer', source: 'tenant' },
      smtpPassword: { isSet: true, source: 'tenant' },
      senderEmail: { value: 'no-reply@example.com', source: 'default' },
      senderName: { value: 'Example', source: 'default' },
    })
  })

  it('reads a null text value as empty and a missing isSet as not set', () => {
    const settings = toEmailSettings(emailSetting({ senderName: { value: null }, smtpPassword: { isSet: null } }))

    expect(settings?.senderName.value).toBe('')
    expect(settings?.smtpPassword.isSet).toBe(false)
  })

  it('answers null when a property is missing', () => {
    const dto = emailSetting()
    dto.properties = dto.properties.filter((candidate) => candidate.name !== 'smtpPort')

    expect(toEmailSettings(dto)).toBeNull()
  })
})
