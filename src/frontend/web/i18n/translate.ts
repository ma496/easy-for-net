/**
 * Resolves one flat, dotted-key lookup against a resources dictionary and applies `${name}`
 * interpolation, returning the key itself when the dictionary has no entry for it - the signal
 * every translator relies on to show a missing message rather than crash or show nothing.
 */
export const translate = (resources: Record<string, string> | undefined, key: string, variables?: Record<string, string | number>): string => {
  let text = resources?.[key] ?? key

  if (variables) {
    for (const [name, value] of Object.entries(variables)) {
      text = text.replace(`\${${name}}`, String(value))
    }
  }

  return text
}
