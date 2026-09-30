/** Counts above this are shown as `99+`, so the badge never grows wider than three characters. */
const BADGE_MAX = 99

/**
 * The text of the bell's unread badge: `null` (no badge at all) when there is nothing unread or the count
 * is not a number, `99+` above {@link BADGE_MAX}, otherwise the whole count.
 */
export function formatUnreadBadge(count: number): string | null {
  if (!Number.isFinite(count) || count <= 0) return null
  if (count > BADGE_MAX) return `${BADGE_MAX}+`
  const whole = Math.floor(count)
  return whole > 0 ? String(whole) : null
}
