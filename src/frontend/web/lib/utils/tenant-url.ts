/** The placeholder a tenant origin template names its tenant's identifier with, as the whole first host label. */
const tenantPlaceholder = '{tenant}'

/**
 * The origin a tenant with `identifier` answers at, built from the API's `tenantUrlTemplate`
 * (`http://{tenant}.example.com`). Returns `null` when there is no template - host resolution is off,
 * so no tenant has an address of its own - or no identifier to put in it. The identifier is trimmed and
 * lower-cased, as the API normalizes it.
 */
export const tenantUrlFromTemplate = (template: string | null | undefined, identifier: string | null | undefined): string | null => {
  const normalized = identifier?.trim().toLowerCase()
  if (!template || !template.includes(tenantPlaceholder) || !normalized) return null
  return template.replace(tenantPlaceholder, normalized)
}

/**
 * Puts an app path (already carrying its locale segment, if any) on another origin, so a link to a
 * tenant's sign-in page is `origin + path` with exactly one slash between them.
 */
export const pathOnOrigin = (origin: string, path: string): string => `${origin.replace(/\/+$/, '')}/${path.replace(/^\/+/, '')}`

/**
 * The sign-in page of the tenant `identifier` names: on its own origin when the template gives it
 * one, on the platform's otherwise - which is where every sign-in happens while host resolution is off.
 * `signinPath` is the locale-aware path of the sign-in page (`/signin`, `/ar/signin`).
 */
export const tenantSigninUrl = (
  { tenantUrlTemplate, platformUrl, identifier }: { tenantUrlTemplate?: string | null; platformUrl: string; identifier: string },
  signinPath: string,
): string => pathOnOrigin(tenantUrlFromTemplate(tenantUrlTemplate, identifier) ?? platformUrl, signinPath)

/** Stands in for the tenant label while a template is parsed as a URL; lower-case, so parsing keeps it. */
const labelStandIn = 'efntenantlabel'

/** Escapes a string for use inside a regular expression. */
const escapeRegExp = (value: string): string => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')

/**
 * Whether `origin` is one of this deployment's own: the platform's, or any tenant's that the API's
 * `tenantUrlTemplate` describes (one host label in place of `{tenant}`). Both addresses come from the
 * API, never from a domain written here; a template or platform address that does not parse admits
 * nothing.
 */
export const isDeploymentOrigin = (origin: string, { platformUrl, tenantUrlTemplate }: { platformUrl?: string | null; tenantUrlTemplate?: string | null }): boolean => {
  try {
    if (platformUrl && new URL(platformUrl).origin === origin) return true
  } catch {
    // An unparseable platform address admits nothing.
  }

  if (!tenantUrlTemplate || !tenantUrlTemplate.includes(tenantPlaceholder)) return false
  try {
    const templateOrigin = new URL(tenantUrlTemplate.replace(tenantPlaceholder, labelStandIn)).origin
    if (!templateOrigin.includes(labelStandIn)) return false
    const pattern = escapeRegExp(templateOrigin).replace(labelStandIn, '[a-z0-9](?:[a-z0-9-]*[a-z0-9])?')
    return new RegExp(`^${pattern}$`, 'i').test(origin)
  } catch {
    return false
  }
}
