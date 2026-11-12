import { describe, expect, it } from 'vitest'
import type { SettingDto, SettingPropertyDto } from './settings-dtos'
import { findSetting, toEmailSettings, toGeneralSettings, toSeoSettings } from './settings-mappers'

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
    property({ name: 'smtpServer', value: 'smtp.example.com', source: 'override', ...overrides.smtpServer }),
    property({ name: 'smtpPort', value: 587, ...overrides.smtpPort }),
    property({ name: 'smtpUsername', value: 'mailer', source: 'override', ...overrides.smtpUsername }),
    property({ name: 'smtpPassword', isSecret: true, isSet: true, source: 'override', ...overrides.smtpPassword }),
    property({ name: 'senderEmail', value: 'no-reply@example.com', ...overrides.senderEmail }),
    property({ name: 'senderName', value: 'Example', ...overrides.senderName }),
  ],
})

describe('findSetting', () => {
  it('finds a setting by its registered name and answers undefined for one the API does not declare', () => {
    const items = [emailSetting(), { name: 'General', properties: [] }]

    expect(findSetting(items, 'Email')?.name).toBe('Email')
    expect(findSetting([emailSetting()], 'General')).toBeUndefined()
    expect(findSetting(undefined, 'General')).toBeUndefined()
  })
})

describe('toGeneralSettings', () => {
  it('reads the flag and where it came from', () => {
    const dto: SettingDto = {
      name: 'General',
      properties: [
        property({ name: 'isEmailVerificationRequired', value: true, source: 'override' }),
        property({ name: 'isSignupEnabled', value: false }),
      ],
    }

    expect(toGeneralSettings(dto)).toEqual({
      isEmailVerificationRequired: { value: true, source: 'override' },
      isSignupEnabled: { value: false, source: 'default' },
    })
  })

  it('answers null when the sign-up flag is missing', () => {
    const dto: SettingDto = { name: 'General', properties: [property({ name: 'isEmailVerificationRequired', value: true })] }

    expect(toGeneralSettings(dto)).toBeNull()
  })

  it('answers null when the property is missing, so the card is dropped rather than guessed', () => {
    expect(toGeneralSettings({ name: 'General', properties: [] })).toBeNull()
  })
})

describe('toEmailSettings', () => {
  it('reads every value with its source and the password as set-or-not only', () => {
    expect(toEmailSettings(emailSetting())).toEqual({
      smtpServer: { value: 'smtp.example.com', source: 'override' },
      smtpPort: { value: 587, source: 'default' },
      smtpUsername: { value: 'mailer', source: 'override' },
      smtpPassword: { isSet: true, source: 'override' },
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

describe('toSeoSettings', () => {
  const seoSetting = (omit?: string): SettingDto => ({
    name: 'Seo',
    properties: [
      property({ name: 'siteName', value: 'Example', source: 'override' }),
      property({ name: 'titleTemplate', value: '%s | Example' }),
      property({ name: 'defaultDescription', value: null }),
      property({ name: 'defaultOgImageFileId', value: '' }),
      property({ name: 'twitterSite', value: '@example' }),
      property({ name: 'siteUrl', value: 'https://www.example.com' }),
      property({ name: 'allowIndexing', value: true }),
      property({ name: 'robotsExtra', value: '' }),
    ].filter((item) => item.name !== omit),
  })

  it('reads every value with its source', () => {
    expect(toSeoSettings(seoSetting())).toMatchObject({
      siteName: { value: 'Example', source: 'override' },
      defaultDescription: { value: '', source: 'default' },
      allowIndexing: { value: true, source: 'default' },
    })
  })

  it('answers null when a property is missing', () => {
    expect(toSeoSettings(seoSetting('siteUrl'))).toBeNull()
  })
})
