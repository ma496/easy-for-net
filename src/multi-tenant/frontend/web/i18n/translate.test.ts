import { describe, expect, it } from 'vitest'
import { humanizeKey, translate } from './translate'

describe('translate', () => {
  it('resolves a flat dotted key from the resources dictionary', () => {
    expect(translate({ 'common.save': 'Save' }, 'common.save')).toBe('Save')
  })

  it("returns an empty string when the dictionary has no entry for it, so the caller's English default applies", () => {
    expect(translate({ 'common.save': 'Save' }, 'common.missing')).toBe('')
    expect(translate({ 'common.save': 'Save' }, 'common.missing') || 'Missing').toBe('Missing')
  })

  it('returns an empty string when the dictionary is undefined', () => {
    expect(translate(undefined, 'common.save')).toBe('')
  })

  it('interpolates every ${name} placeholder from the given variables', () => {
    expect(translate({ greeting: 'Hello ${name}, you have ${count} messages' }, 'greeting', { name: 'Ann', count: 3 })).toBe('Hello Ann, you have 3 messages')
  })

  it('leaves an unmatched placeholder untouched when no variable names it', () => {
    expect(translate({ greeting: 'Hello ${name}' }, 'greeting', { other: 'x' })).toBe('Hello ${name}')
  })
})

describe('humanizeKey', () => {
  it.each([
    ['PartiallyReturned', 'Partially returned'],
    ['Cash', 'Cash'],
    ['nav.taxRates', 'Tax rates'],
    ['last7', 'Last 7'],
    [3, '3'],
    ['', ''],
  ])('turns %s into %s', (value, expected) => {
    expect(humanizeKey(value)).toBe(expected)
  })
})
