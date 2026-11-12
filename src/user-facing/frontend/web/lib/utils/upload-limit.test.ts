import { describe, expect, it } from 'vitest'
import { formatMegabytes } from './upload-limit'

describe('formatMegabytes', () => {
  it('shows whole megabytes without a decimal', () => {
    expect(formatMegabytes(1024 * 1024)).toBe('1')
  })

  it('shows at most one decimal place', () => {
    expect(formatMegabytes(2.5 * 1024 * 1024)).toBe('2.5')
    expect(formatMegabytes(1.26 * 1024 * 1024)).toBe('1.3')
  })
})
