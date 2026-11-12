/** The largest unit a span is expressed in, with its length in seconds, from the coarsest down. */
const units: [Intl.RelativeTimeFormatUnit, number][] = [
  ['year', 31_536_000],
  ['month', 2_592_000],
  ['week', 604_800],
  ['day', 86_400],
  ['hour', 3_600],
  ['minute', 60],
]

/**
 * Describes how long ago (or how far ahead) a moment is, in the given culture - "3 days ago", "قبل 3 أيام",
 * "il y a 3 jours" - using the coarsest unit the span reaches. `now` is a parameter so the wording can be tested.
 */
export function formatRelativeTime(date: Date | string, culture: string, now: Date = new Date()): string {
  const seconds = (new Date(date).getTime() - now.getTime()) / 1000
  const format = new Intl.RelativeTimeFormat(culture, { numeric: 'auto' })
  for (const [unit, length] of units) {
    if (Math.abs(seconds) >= length) return format.format(Math.round(seconds / length), unit)
  }
  return format.format(Math.round(seconds), 'second')
}
