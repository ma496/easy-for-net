/**
 * Awaits an object of pending translations at once and hands back the same keys with their text, so a
 * server section can name each string rather than destructure a long positional array. Each value is
 * written at its call site as `getServerTranslation(lang, 'key').then((text) => text || 'English')`, so
 * every literal key keeps its English default where `i18n/default-texts.test.ts` can see it.
 */
export const resolveTexts = async <T extends Record<string, Promise<string>>>(texts: T): Promise<{ [K in keyof T]: string }> => {
  const entries = await Promise.all(Object.entries(texts).map(async ([name, text]) => [name, await text] as const))
  return Object.fromEntries(entries) as { [K in keyof T]: string }
}
