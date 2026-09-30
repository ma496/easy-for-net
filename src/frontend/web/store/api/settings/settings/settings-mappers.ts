import {
  EmailSettingsDto,
  SecretSettingState,
  SettingDto,
  SettingJsonValue,
  SettingName,
  SettingPropertyDto,
  SettingValueWithSource,
  SigninSettingsDto,
} from './settings-dtos'

/** The setting of that name in a list response, or `undefined` when the API does not declare it. */
export const findSetting = (items: SettingDto[] | undefined, name: SettingName): SettingDto | undefined =>
  items?.find((item) => item.name === name)

const propertyOf = (dto: SettingDto, name: string): SettingPropertyDto | undefined => dto.properties.find((property) => property.name === name)

/** Reads one property with a coercion, or `null` when the setting does not carry it. */
const read = <T>(dto: SettingDto, name: string, coerce: (value: SettingJsonValue) => T): SettingValueWithSource<T> | null => {
  const property = propertyOf(dto, name)
  return property ? { value: coerce(property.value), source: property.source } : null
}

const asString = (value: SettingJsonValue): string => (typeof value === 'string' ? value : value === null ? '' : String(value))
const asBoolean = (value: SettingJsonValue): boolean => value === true
const asNumber = (value: SettingJsonValue): number => (typeof value === 'number' ? value : Number(value))

const readSecret = (dto: SettingDto, name: string): SecretSettingState | null => {
  const property = propertyOf(dto, name)
  return property ? { isSet: property.isSet === true, source: property.source } : null
}

/** The `Signin` setting as a typed view model, or `null` when the response lacks one of its properties. */
export const toSigninSettings = (dto: SettingDto): SigninSettingsDto | null => {
  const isEmailVerificationRequired = read(dto, 'isEmailVerificationRequired', asBoolean)
  return isEmailVerificationRequired ? { isEmailVerificationRequired } : null
}

/** The `Email` setting as a typed view model, or `null` when the response lacks one of its properties. */
export const toEmailSettings = (dto: SettingDto): EmailSettingsDto | null => {
  const smtpServer = read(dto, 'smtpServer', asString)
  const smtpPort = read(dto, 'smtpPort', asNumber)
  const smtpUsername = read(dto, 'smtpUsername', asString)
  const smtpPassword = readSecret(dto, 'smtpPassword')
  const senderEmail = read(dto, 'senderEmail', asString)
  const senderName = read(dto, 'senderName', asString)

  if (!smtpServer || !smtpPort || !smtpUsername || !smtpPassword || !senderEmail || !senderName) return null
  return { smtpServer, smtpPort, smtpUsername, smtpPassword, senderEmail, senderName }
}
