import type { EmailSettingsDto, SettingSource, SigninSettingsDto } from '@/store/api/settings'

/**
 * How a property's value reads to the administrator on this screen: set by the scope being edited,
 * inherited from the platform, or the default.
 */
export type SourceMarker = 'own' | 'platform' | 'default'

/**
 * The layer the scope being edited writes to: the tenant's own row while acting inside a tenant, the
 * platform's while acting in none.
 */
export const ownSourceOf = (hasActiveTenant: boolean): SettingSource => (hasActiveTenant ? 'tenant' : 'platform')

/** The marker a property's source earns, seen from the scope being edited. */
export const markerOf = (source: SettingSource, ownSource: SettingSource): SourceMarker => {
  if (source === ownSource) return 'own'
  if (source === 'default') return 'default'
  return 'platform'
}

/** Whether any of the given sources is the scope's own - what decides "Customized here" and the Reset button. */
export const hasOwnOverride = (sources: SettingSource[], ownSource: SettingSource): boolean => sources.includes(ownSource)

/** The sources of every property of the `Signin` setting. */
export const signinSources = (settings: SigninSettingsDto): SettingSource[] => [settings.isEmailVerificationRequired.source]

/** The sources of every property of the `Email` setting, the password included. */
export const emailSources = (settings: EmailSettingsDto): SettingSource[] => [
  settings.smtpServer.source,
  settings.smtpPort.source,
  settings.smtpUsername.source,
  settings.smtpPassword.source,
  settings.senderEmail.source,
  settings.senderName.source,
]

/** The Sign-in card's form values. */
export interface SigninFormValues {
  isEmailVerificationRequired: boolean
}

/**
 * The Email card's form values. The port starts as text, but a number input hands Formik a number once
 * typed (or `''` when emptied), so it is read through `portText`. The password always starts empty.
 */
export interface EmailFormValues {
  smtpServer: string
  smtpPort: string | number
  smtpUsername: string
  /** What the administrator typed - empty means "not touched". */
  smtpPassword: string
  /** Set by the Clear button: the scope's own password is removed on save. */
  smtpPasswordCleared: boolean
  senderEmail: string
  senderName: string
}

/** The Sign-in form seeded from the resolved setting. */
export const signinFormValues = (settings: SigninSettingsDto): SigninFormValues => ({
  isEmailVerificationRequired: settings.isEmailVerificationRequired.value,
})

/** The Email form seeded from the resolved setting. */
export const emailFormValues = (settings: EmailSettingsDto): EmailFormValues => ({
  smtpServer: settings.smtpServer.value,
  smtpPort: String(settings.smtpPort.value),
  smtpUsername: settings.smtpUsername.value,
  smtpPassword: '',
  smtpPasswordCleared: false,
  senderEmail: settings.senderEmail.value,
  senderName: settings.senderName.value,
})

/** The port as trimmed text, whichever shape the input handed over. */
export const portText = (value: string | number | null | undefined): string => (value === null || value === undefined ? '' : String(value).trim())

/** Whether the port is a whole number the API will accept. */
export const isValidPort = (value: string | number | null | undefined): boolean => {
  const trimmed = portText(value)
  if (!/^\d+$/.test(trimmed)) return false
  const port = Number(trimmed)
  return port >= 1 && port <= 65535
}

/** The `PUT /settings/Signin` values: what this scope overrides. */
export type SigninUpdateValues = { isEmailVerificationRequired?: boolean }

/** The `PUT /settings/Email` values: what this scope overrides, the password by its own rules. */
export type EmailUpdateValues = {
  smtpServer?: string
  smtpPort?: number
  smtpUsername?: string
  smtpPassword?: string
  senderEmail?: string
  senderName?: string
}

/**
 * Whether a property belongs in the replace-semantics body: the scope already overrides it (leaving it
 * out would stop overriding it), or the administrator changed it here.
 */
const inOverrideSet = (source: SettingSource, ownSource: SettingSource, changed: boolean): boolean => source === ownSource || changed

/** The `PUT` body for the Sign-in card. */
export const buildSigninUpdateValues = (
  settings: SigninSettingsDto,
  initial: SigninFormValues,
  current: SigninFormValues,
  ownSource: SettingSource,
): SigninUpdateValues => {
  const values: SigninUpdateValues = {}
  const changed = current.isEmailVerificationRequired !== initial.isEmailVerificationRequired
  if (inOverrideSet(settings.isEmailVerificationRequired.source, ownSource, changed)) {
    values.isEmailVerificationRequired = current.isEmailVerificationRequired
  }
  return values
}

/**
 * The `PUT` body for the Email card. Every non-secret property the scope overrides or the
 * administrator changed is sent, the port as a number. The password follows the API's secret rules
 * instead: sent when something was typed, sent as `""` when Clear was pressed, and otherwise left out
 * altogether - which keeps whatever this scope has stored - so an untouched field never sends an empty
 * or null value.
 */
export const buildEmailUpdateValues = (
  settings: EmailSettingsDto,
  initial: EmailFormValues,
  current: EmailFormValues,
  ownSource: SettingSource,
): EmailUpdateValues => {
  const values: EmailUpdateValues = {}

  if (inOverrideSet(settings.smtpServer.source, ownSource, current.smtpServer !== initial.smtpServer)) values.smtpServer = current.smtpServer
  if (inOverrideSet(settings.smtpPort.source, ownSource, portText(current.smtpPort) !== portText(initial.smtpPort))) values.smtpPort = Number(portText(current.smtpPort))
  if (inOverrideSet(settings.smtpUsername.source, ownSource, current.smtpUsername !== initial.smtpUsername)) values.smtpUsername = current.smtpUsername
  if (inOverrideSet(settings.senderEmail.source, ownSource, current.senderEmail !== initial.senderEmail)) values.senderEmail = current.senderEmail
  if (inOverrideSet(settings.senderName.source, ownSource, current.senderName !== initial.senderName)) values.senderName = current.senderName

  if (current.smtpPasswordCleared) values.smtpPassword = ''
  else if (current.smtpPassword.length > 0) values.smtpPassword = current.smtpPassword

  return values
}
