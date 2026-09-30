import { describe, expect, it } from 'vitest'
import {
  fallbackPollDelayMs,
  FALLBACK_POLL_INTERVAL_MS,
  reconnectDelayMs,
  RECONNECT_BASE_DELAY_MS,
  RECONNECT_MAX_DELAY_MS,
} from './hub-reconnect'

const lowest = () => 0
const highest = () => 0.999_999

describe('reconnectDelayMs', () => {
  it('waits between half and all of the base delay before the first retry', () => {
    expect(reconnectDelayMs(0, lowest)).toBe(RECONNECT_BASE_DELAY_MS / 2)
    expect(reconnectDelayMs(0, highest)).toBe(RECONNECT_BASE_DELAY_MS)
  })

  it('doubles its ceiling on every failure until the cap', () => {
    const ceilings = [0, 1, 2, 3, 4, 5].map((n) => reconnectDelayMs(n, highest))
    expect(ceilings).toEqual([1_000, 2_000, 4_000, 8_000, 16_000, 32_000])
    expect(reconnectDelayMs(6, highest)).toBe(RECONNECT_MAX_DELAY_MS)
  })

  it('keeps every delay within the jitter bounds of its ceiling', () => {
    for (let n = 0; n < 12; n++) {
      const ceiling = Math.min(RECONNECT_MAX_DELAY_MS, RECONNECT_BASE_DELAY_MS * 2 ** n)
      for (const r of [0, 0.25, 0.5, 0.75, 0.999]) {
        const delay = reconnectDelayMs(n, () => r)
        expect(delay).toBeGreaterThanOrEqual(ceiling / 2)
        expect(delay).toBeLessThanOrEqual(ceiling)
      }
    }
  })

  it('varies with the random source, so clients spread out', () => {
    expect(reconnectDelayMs(3, () => 0.1)).not.toBe(reconnectDelayMs(3, () => 0.9))
  })

  it('never exceeds 60 seconds and never gives up, however long the outage', () => {
    for (const n of [7, 50, 1_000, 10_000, Number.MAX_SAFE_INTEGER]) {
      const delay = reconnectDelayMs(n, highest)
      expect(delay).not.toBeNull()
      expect(delay).toBeLessThanOrEqual(RECONNECT_MAX_DELAY_MS)
      expect(delay).toBeGreaterThanOrEqual(RECONNECT_MAX_DELAY_MS / 2)
    }
  })

  it('treats a nonsensical retry count as the first retry', () => {
    for (const n of [-1, Number.NaN, Number.NEGATIVE_INFINITY]) {
      expect(reconnectDelayMs(n, highest)).toBe(RECONNECT_BASE_DELAY_MS)
    }
  })

  it('stays in bounds when the random source misbehaves', () => {
    for (const r of [-5, 1, 7, Number.NaN]) {
      const delay = reconnectDelayMs(20, () => r)
      expect(delay).toBeGreaterThanOrEqual(RECONNECT_MAX_DELAY_MS / 2)
      expect(delay).toBeLessThanOrEqual(RECONNECT_MAX_DELAY_MS)
    }
  })
})

describe('fallbackPollDelayMs', () => {
  it('polls about once a minute, within ten percent either way', () => {
    expect(fallbackPollDelayMs(lowest)).toBe(FALLBACK_POLL_INTERVAL_MS * 0.9)
    expect(fallbackPollDelayMs(() => 0.5)).toBe(FALLBACK_POLL_INTERVAL_MS)
    expect(fallbackPollDelayMs(highest)).toBe(FALLBACK_POLL_INTERVAL_MS * 1.1)
  })

  it('stays in bounds when the random source misbehaves', () => {
    for (const r of [-1, 2, Number.NaN]) {
      const delay = fallbackPollDelayMs(() => r)
      expect(delay).toBeGreaterThanOrEqual(FALLBACK_POLL_INTERVAL_MS * 0.9)
      expect(delay).toBeLessThanOrEqual(FALLBACK_POLL_INTERVAL_MS * 1.1)
    }
  })
})
