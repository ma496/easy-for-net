import { describe, expect, it } from 'vitest'
import { translate } from './translate'

describe('translate', () => {
  it('resolves a flat dotted key from the resources dictionary', () => {
    expect(translate({ 'common.save': 'Save' }, 'common.save')).toBe('Save')
  })

  it('returns the key itself when the dictionary has no entry for it', () => {
    expect(translate({ 'common.save': 'Save' }, 'common.missing')).toBe('common.missing')
  })

  it('returns the key itself when the dictionary is undefined', () => {
    expect(translate(undefined, 'common.save')).toBe('common.save')
  })

  it('interpolates every ${name} placeholder from the given variables', () => {
    expect(translate({ greeting: 'Hello ${name}, you have ${count} messages' }, 'greeting', { name: 'Ann', count: 3 })).toBe('Hello Ann, you have 3 messages')
  })

  it('leaves an unmatched placeholder untouched when no variable names it', () => {
    expect(translate({ greeting: 'Hello ${name}' }, 'greeting', { other: 'x' })).toBe('Hello ${name}')
  })
})
