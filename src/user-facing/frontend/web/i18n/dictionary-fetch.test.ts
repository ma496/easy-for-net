import { describe, expect, it } from 'vitest'
import { dictionaryFetchInit } from './server'

describe('dictionaryFetchInit', () => {
  it('caches the anonymous dictionary under the localization tag and sends no cookies', () => {
    expect(dictionaryFetchInit(false)).toEqual({ forwardCookies: false, next: { revalidate: 300, tags: ['localization'] } })
  })

  it('fetches a signed-in visitor dictionary afresh, with their cookies', () => {
    expect(dictionaryFetchInit(true)).toEqual({ forwardCookies: true, cache: 'no-store' })
  })
})
