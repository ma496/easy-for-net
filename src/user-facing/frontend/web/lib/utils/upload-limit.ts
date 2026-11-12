/**
 * Formats a byte limit as megabytes for a message, dropping a trailing `.0` - `1048576` reads `1`,
 * `2621440` reads `2.5`.
 *
 * @param bytes The limit in bytes.
 * @returns The limit in megabytes, to at most one decimal place.
 */
export const formatMegabytes = (bytes: number): string => String(Math.round((bytes / (1024 * 1024)) * 10) / 10)
