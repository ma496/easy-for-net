/**
 * Reads a notification's metadata as the `${name}` variables its title and message are translated
 * with, so a notification raised as "You have been added to ${tenantName}" shows the tenant it names.
 * Only the top-level string and number values of a JSON object qualify; metadata that is missing, is
 * not valid JSON or is not an object yields no variables, and the text is shown as translated.
 */
export const notificationVariables = (metadata: string | null | undefined): Record<string, string | number> | undefined => {
  if (!metadata) {
    return undefined
  }

  let parsed: unknown
  try {
    parsed = JSON.parse(metadata)
  } catch {
    return undefined
  }

  if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed)) {
    return undefined
  }

  const variables: Record<string, string | number> = {}
  for (const [name, value] of Object.entries(parsed)) {
    if (typeof value === 'string' || typeof value === 'number') {
      variables[name] = value
    }
  }

  return variables
}
