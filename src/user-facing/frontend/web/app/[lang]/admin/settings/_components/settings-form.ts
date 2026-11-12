import type { EmailSettingsDto, SettingSource, GeneralSettingsDto, SeoSettingsDto } from '@/store/api/settings'

/** How a property's value reads to the administrator on this screen: set here, or the default. */
export type SourceMarker = 'own' | 'default'

/**
 * Whether a source is the site's own override. Anything but the default layer counts, so the screen
 * keeps working whatever the API calls the layer it writes to.
 */
const isOwn = (source: SettingSource): boolean => source !== 'default'

/** The marker a property's source earns. */
export const markerOf = (source: SettingSource): SourceMarker => (isOwn(source) ? 'own' : 'default')

/** Whether any of the given sources is the site's own - what decides "Customized" and the Reset button. */
export const hasOwnOverride = (sources: SettingSource[]): boolean => sources.some(isOwn)

/** The sources of every property of the `General` setting. */
export const generalSources = (settings: GeneralSettingsDto): SettingSource[] => [settings.isEmailVerificationRequired.source, settings.isSignupEnabled.source]

/** The sources of every property of the `Email` setting, the password included. */
export const emailSources = (settings: EmailSettingsDto): SettingSource[] => [
  settings.smtpServer.source,
  settings.smtpPort.source,
  settings.smtpUsername.source,
  settings.smtpPassword.source,
  settings.senderEmail.source,
  settings.senderName.source,
]

/** The General card's form values. */
export interface GeneralFormValues {
  isEmailVerificationRequired: boolean
  isSignupEnabled: boolean
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

/** The General form seeded from the resolved setting. */
export const generalFormValues = (settings: GeneralSettingsDto): GeneralFormValues => ({
  isEmailVerificationRequired: settings.isEmailVerificationRequired.value,
  isSignupEnabled: settings.isSignupEnabled.value,
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

/** The `PUT /settings/General` values: what the site overrides. */
export type GeneralUpdateValues = { isEmailVerificationRequired?: boolean; isSignupEnabled?: boolean }

/** The `PUT /settings/Email` values: what the site overrides, the password by its own rules. */
export type EmailUpdateValues = {
  smtpServer?: string
  smtpPort?: number
  smtpUsername?: string
  smtpPassword?: string
  senderEmail?: string
  senderName?: string
}

/**
 * Whether a property belongs in the replace-semantics body: the site already overrides it (leaving it
 * out would stop overriding it), or the administrator changed it here.
 */
const inOverrideSet = (source: SettingSource, changed: boolean): boolean => isOwn(source) || changed

/** The `PUT` body for the General card. */
export const buildGeneralUpdateValues = (settings: GeneralSettingsDto, initial: GeneralFormValues, current: GeneralFormValues): GeneralUpdateValues => {
  const values: GeneralUpdateValues = {}
  if (inOverrideSet(settings.isEmailVerificationRequired.source, current.isEmailVerificationRequired !== initial.isEmailVerificationRequired)) {
    values.isEmailVerificationRequired = current.isEmailVerificationRequired
  }
  if (inOverrideSet(settings.isSignupEnabled.source, current.isSignupEnabled !== initial.isSignupEnabled)) {
    values.isSignupEnabled = current.isSignupEnabled
  }
  return values
}

/**
 * The `PUT` body for the Email card. Every non-secret property the scope overrides or the
 * administrator changed is sent, the port as a number. The password follows the API's secret rules
 * instead: sent when something was typed, sent as `""` when Clear was pressed, and otherwise left out
 * altogether - which keeps whatever the site has stored - so an untouched field never sends an empty
 * or null value.
 */
export const buildEmailUpdateValues = (
  settings: EmailSettingsDto,
  initial: EmailFormValues,
  current: EmailFormValues,
): EmailUpdateValues => {
  const values: EmailUpdateValues = {}

  if (inOverrideSet(settings.smtpServer.source, current.smtpServer !== initial.smtpServer)) values.smtpServer = current.smtpServer
  if (inOverrideSet(settings.smtpPort.source, portText(current.smtpPort) !== portText(initial.smtpPort))) values.smtpPort = Number(portText(current.smtpPort))
  if (inOverrideSet(settings.smtpUsername.source, current.smtpUsername !== initial.smtpUsername)) values.smtpUsername = current.smtpUsername
  if (inOverrideSet(settings.senderEmail.source, current.senderEmail !== initial.senderEmail)) values.senderEmail = current.senderEmail
  if (inOverrideSet(settings.senderName.source, current.senderName !== initial.senderName)) values.senderName = current.senderName

  if (current.smtpPasswordCleared) values.smtpPassword = ''
  else if (current.smtpPassword.length > 0) values.smtpPassword = current.smtpPassword

  return values
}

/** The sources of every property of the `Seo` setting. */
export const seoSources = (settings: SeoSettingsDto): SettingSource[] => Object.values(settings).map((property) => property.source)

/** The SEO card's form values. */
export interface SeoFormValues {
  siteName: string
  titleTemplate: string
  defaultDescription: string
  /** A stored file's id, or `''` for none. */
  defaultOgImageFileId: string
  twitterSite: string
  siteUrl: string
  allowIndexing: boolean
  robotsExtra: string
}

/** The SEO form seeded from the resolved setting. */
export const seoFormValues = (settings: SeoSettingsDto): SeoFormValues => ({
  siteName: settings.siteName.value,
  titleTemplate: settings.titleTemplate.value,
  defaultDescription: settings.defaultDescription.value,
  defaultOgImageFileId: settings.defaultOgImageFileId.value,
  twitterSite: settings.twitterSite.value,
  siteUrl: settings.siteUrl.value,
  allowIndexing: settings.allowIndexing.value,
  robotsExtra: settings.robotsExtra.value,
})

/** The `PUT /settings/Seo` values: what the site overrides. */
export type SeoUpdateValues = Partial<SeoFormValues>

/** Whether a title template is usable: it must carry `%s`, where each page's own title goes. */
export const isValidTitleTemplate = (value: string | null | undefined): boolean => !!value && value.includes('%s')

/** Whether a site address is an absolute http(s) origin with no path or query (a trailing slash is dropped on save) - empty counts as "not set". */
export const isValidSiteUrl = (value: string | null | undefined): boolean => {
  const trimmed = (value ?? '').trim()
  if (!trimmed) return true
  try {
    const url = new URL(trimmed)
    return (url.protocol === 'http:' || url.protocol === 'https:') && url.origin === trimmed.replace(/\/+$/, '')
  } catch {
    return false
  }
}

/** The `PUT` body for the SEO card: every property the site overrides or the administrator changed, the site address without a trailing slash. */
export const buildSeoUpdateValues = (settings: SeoSettingsDto, initial: SeoFormValues, current: SeoFormValues): SeoUpdateValues => {
  const values: Record<string, string | boolean> = {}
  for (const field of Object.keys(initial) as (keyof SeoFormValues)[]) {
    const value = field === 'siteUrl' ? current.siteUrl.trim().replace(/\/+$/, '') : current[field]
    if (inOverrideSet(settings[field].source, value !== initial[field])) values[field] = value
  }
  return values as SeoUpdateValues
}
