import { describe, expect, it } from 'vitest'
import { formatRelativeTime } from './relative-time'

const now = new Date('2026-10-04T12:00:00Z')
const ago = (seconds: number) => new Date(now.getTime() - seconds * 1000)

describe('formatRelativeTime', () => {
  it('uses the coarsest unit the span reaches', () => {
    expect(formatRelativeTime(ago(30), 'en', now)).toBe('30 seconds ago')
    expect(formatRelativeTime(ago(5 * 60), 'en', now)).toBe('5 minutes ago')
    expect(formatRelativeTime(ago(3 * 3600), 'en', now)).toBe('3 hours ago')
    expect(formatRelativeTime(ago(3 * 86_400), 'en', now)).toBe('3 days ago')
  })

  it('words a single unit naturally', () => {
    expect(formatRelativeTime(ago(86_400), 'en', now)).toBe('yesterday')
  })

  it('speaks the culture it is given', () => {
    expect(formatRelativeTime(ago(3 * 86_400), 'fr', now)).toBe('il y a 3 jours')
    expect(formatRelativeTime(ago(3 * 86_400), 'es', now)).toBe('hace 3 días')
  })

  it('accepts an ISO string as the API sends it', () => {
    expect(formatRelativeTime(ago(2 * 3600).toISOString(), 'en', now)).toBe('2 hours ago')
  })
})
