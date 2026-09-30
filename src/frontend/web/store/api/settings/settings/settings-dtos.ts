import { RequestBase } from '@/store/api'

/** The layer a setting property's effective value came from, as the API spells it. */
export const SettingSource = {
  /** The acting tenant's own override. */
  Tenant: 'tenant',
  /** The platform's override, which every tenant inherits unless it sets its own. */
  Platform: 'platform',
  /** The setting's default - the deployment's configured one, else the code default. */
  Default: 'default',
} as const

/** The layer a setting property's effective value came from. */
export type SettingSource = (typeof SettingSource)[keyof typeof SettingSource]

/** The names settings are registered, stored and addressed under on the API. */
export const SettingName = {
  Signin: 'Signin',
  Email: 'Email',
} as const

/** A registered setting name. */
export type SettingName = (typeof SettingName)[keyof typeof SettingName]

/** Any JSON value a setting property may hold on the wire. */
export type SettingJsonValue = string | number | boolean | null | SettingJsonValue[] | { [key: string]: SettingJsonValue }

/** One value of a resolved setting and the layer it came from (`SettingPropertyDto` on the API). */
export interface SettingPropertyDto {
  name: string
  /** The effective value - always `null` for a secret. */
  value: SettingJsonValue
  source: SettingSource
  isSecret: boolean
  /** For a secret, whether its effective value is non-empty; `null` for any other property. */
  isSet: boolean | null
}

/** One setting as the acting scope resolves it, property by property (`SettingDto` on the API). */
export interface SettingDto {
  name: string
  properties: SettingPropertyDto[]
}

/** Response of `GET /settings`: every declared setting as the acting scope resolves it. */
export interface SettingListResponse {
  items: SettingDto[]
}

/**
 * The properties a scope overrides, keyed by property name. A key left `undefined` is dropped when the
 * body is serialized, which for a secret means "keep the stored value".
 */
export type SettingValues = { [property: string]: SettingJsonValue | undefined }

/** Request body of `PUT /settings/{name}`: every property the acting scope overrides, and only those. */
export interface SettingUpdateRequest extends RequestBase {
  name: string
  values: SettingValues
}

/** Request of `DELETE /settings/{name}`, removing the acting scope's overrides of one setting. */
export interface SettingDeleteRequest extends RequestBase {
  name: string
}

/** A non-secret property's effective value together with the layer it came from. */
export interface SettingValueWithSource<T> {
  value: T
  source: SettingSource
}

/** A secret property: never its value, only whether one is in force and where it came from. */
export interface SecretSettingState {
  isSet: boolean
  source: SettingSource
}

/** The `Signin` setting, typed. */
export interface SigninSettingsDto {
  isEmailVerificationRequired: SettingValueWithSource<boolean>
}

/** The `Email` setting, typed. The password is a secret and carries no value. */
export interface EmailSettingsDto {
  smtpServer: SettingValueWithSource<string>
  smtpPort: SettingValueWithSource<number>
  smtpUsername: SettingValueWithSource<string>
  smtpPassword: SecretSettingState
  senderEmail: SettingValueWithSource<string>
  senderName: SettingValueWithSource<string>
}
