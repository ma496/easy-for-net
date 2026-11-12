import { RequestBase } from '@/store/api'

/** The layer a setting property's effective value came from, as the API spells it. */
export const SettingSource = {
  /** The site's own override, set on the settings screen. */
  Override: 'override',
  /** The setting's default - the deployment's configured one, else the code default. */
  Default: 'default',
} as const

/** The layer a setting property's effective value came from. */
export type SettingSource = (typeof SettingSource)[keyof typeof SettingSource]

/** The names settings are registered, stored and addressed under on the API. */
export const SettingName = {
  General: 'General',
  Email: 'Email',
  Seo: 'Seo',
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

/** One setting as the site resolves it, property by property (`SettingDto` on the API). */
export interface SettingDto {
  name: string
  properties: SettingPropertyDto[]
}

/** Response of `GET /settings`: every declared setting as the site resolves it. */
export interface SettingListResponse {
  items: SettingDto[]
}

/**
 * The properties the site overrides, keyed by property name. A key left `undefined` is dropped when the
 * body is serialized, which for a secret means "keep the stored value".
 */
export type SettingValues = { [property: string]: SettingJsonValue | undefined }

/** Request body of `PUT /settings/{name}`: every property the site overrides, and only those. */
export interface SettingUpdateRequest extends RequestBase {
  name: string
  values: SettingValues
}

/** Request of `DELETE /settings/{name}`, removing the site's overrides of one setting, so its defaults apply. */
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

/** The `General` setting, typed. */
export interface GeneralSettingsDto {
  isEmailVerificationRequired: SettingValueWithSource<boolean>
  /** Whether anyone may create an account from the sign-up page. */
  isSignupEnabled: SettingValueWithSource<boolean>
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

/** The `Seo` setting, typed: the site-wide defaults every page's metadata falls back to. */
export interface SeoSettingsDto {
  siteName: SettingValueWithSource<string>
  /** The page title pattern; must contain `%s`, which the page's own title replaces. */
  titleTemplate: SettingValueWithSource<string>
  defaultDescription: SettingValueWithSource<string>
  /** A stored file's id (from the file upload endpoint), or `''` for none. */
  defaultOgImageFileId: SettingValueWithSource<string>
  twitterSite: SettingValueWithSource<string>
  /** The site's public origin, absolute, with no trailing slash. */
  siteUrl: SettingValueWithSource<string>
  allowIndexing: SettingValueWithSource<boolean>
  /** Extra `robots.txt` lines, one per line. */
  robotsExtra: SettingValueWithSource<string>
}
