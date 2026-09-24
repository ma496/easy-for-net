/**
 * Converts a plan's `FileManagement.MaxFileSizeMb` value into bytes.
 *
 * @param megabytes The feature value as the API reports it, or `null` when nothing supplied one.
 * @returns The limit in bytes, or `undefined` when the value imposes no limit. A value that is not a
 * positive whole number imposes none, matching the API, which ignores a value it cannot read as one.
 */
export const planMegabytesToBytes = (megabytes: string | null | undefined): number | undefined => {
  if (megabytes == null || !/^\d+$/.test(megabytes.trim())) return undefined
  const value = Number(megabytes)
  return value > 0 ? value * 1024 * 1024 : undefined
}

/**
 * The upload limit actually in force: the stricter of the one a component was given and the one the
 * caller's plan sets.
 *
 * @param componentLimit The limit the component was configured with, if any.
 * @param planLimit The plan's limit in bytes, if any.
 * @returns The smaller of the two, or whichever one is set, or `undefined` when neither is.
 */
export const effectiveMaxUploadBytes = (componentLimit: number | undefined, planLimit: number | undefined): number | undefined => {
  if (componentLimit === undefined) return planLimit
  if (planLimit === undefined) return componentLimit
  return Math.min(componentLimit, planLimit)
}

/**
 * Formats a byte limit as megabytes for a message, dropping a trailing `.0` - `1048576` reads `1`,
 * `2621440` reads `2.5`.
 *
 * @param bytes The limit in bytes.
 * @returns The limit in megabytes, to at most one decimal place.
 */
export const formatMegabytes = (bytes: number): string => String(Math.round((bytes / (1024 * 1024)) * 10) / 10)
