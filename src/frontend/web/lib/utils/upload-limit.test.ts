import { describe, expect, it } from 'vitest'
import { effectiveMaxUploadBytes, formatMegabytes, planMegabytesToBytes } from './upload-limit'

describe('planMegabytesToBytes', () => {
  it('converts whole megabytes to bytes', () => {
    expect(planMegabytesToBytes('25')).toBe(25 * 1024 * 1024)
  })

  it.each([null, undefined, '', 'abc', '1.5', '-3', '0'])('imposes no limit for %j', (value) => {
    expect(planMegabytesToBytes(value)).toBeUndefined()
  })
})

describe('effectiveMaxUploadBytes', () => {
  it('takes the stricter of the two limits', () => {
    expect(effectiveMaxUploadBytes(10, 5)).toBe(5)
    expect(effectiveMaxUploadBytes(5, 10)).toBe(5)
  })

  it('falls back to whichever limit is set', () => {
    expect(effectiveMaxUploadBytes(undefined, 5)).toBe(5)
    expect(effectiveMaxUploadBytes(5, undefined)).toBe(5)
    expect(effectiveMaxUploadBytes(undefined, undefined)).toBeUndefined()
  })
})

describe('formatMegabytes', () => {
  it('shows whole megabytes without a decimal', () => {
    expect(formatMegabytes(1024 * 1024)).toBe('1')
  })

  it('shows at most one decimal place', () => {
    expect(formatMegabytes(2.5 * 1024 * 1024)).toBe('2.5')
    expect(formatMegabytes(1.26 * 1024 * 1024)).toBe('1.3')
  })
})
