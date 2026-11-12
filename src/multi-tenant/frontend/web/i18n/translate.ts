/**
 * Resolves one flat, dotted-key lookup against a resources dictionary and applies `${name}`
 * interpolation, returning `''` when the dictionary has no entry for it. Every caller supplies the
 * English text after `||` - `t('common.save') || 'Save'` - so a key the API did not serve (or an API
 * that could not be reached at all) still renders readable English rather than the raw key.
 */
export const translate = (resources: Record<string, string> | undefined, key: string, variables?: Record<string, string | number>): string => {
  let text = resources?.[key] ?? ''

  if (variables) {
    for (const [name, value] of Object.entries(variables)) {
      text = text.replace(`\${${name}}`, String(value))
    }
  }

  return text
}

/**
 * The English fallback for a key that is only known at run time - an enum value
 * (`` t(`page.sales.status.${status}`) || humanizeKey(status) ``) or a key held in data
 * (`t(item.title) || humanizeKey(item.title)`): the last dotted segment split into words and
 * sentence-cased, so `PartiallyReturned` reads "Partially returned" and `nav.taxRates` "Tax rates".
 */
export const humanizeKey = (value: string | number): string => {
  const words = (String(value).split('.').pop() ?? '')
    .replace(/([a-z\d])([A-Z])/g, '$1 $2')
    .replace(/([a-zA-Z])(\d)/g, '$1 $2')
    .toLowerCase()
  return words.charAt(0).toUpperCase() + words.slice(1)
}
