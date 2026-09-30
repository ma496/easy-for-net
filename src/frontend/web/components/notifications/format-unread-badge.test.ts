import { describe, expect, it } from 'vitest'
import { formatUnreadBadge } from './format-unread-badge'

describe('formatUnreadBadge', () => {
  it('shows no badge when nothing is unread or the count is not a number', () => {
    for (const count of [-1, 0, Number.NaN, Number.POSITIVE_INFINITY, Number.NEGATIVE_INFINITY, 0.5]) {
      expect(formatUnreadBadge(count)).toBeNull()
    }
  })

  it('shows the count itself up to 99', () => {
    expect(formatUnreadBadge(1)).toBe('1')
    expect(formatUnreadBadge(9)).toBe('9')
    expect(formatUnreadBadge(10)).toBe('10')
    expect(formatUnreadBadge(99)).toBe('99')
  })

  it('shows a whole number for a fractional count', () => {
    expect(formatUnreadBadge(3.7)).toBe('3')
  })

  it('caps the badge at 99+', () => {
    expect(formatUnreadBadge(100)).toBe('99+')
    expect(formatUnreadBadge(12345)).toBe('99+')
  })
})
