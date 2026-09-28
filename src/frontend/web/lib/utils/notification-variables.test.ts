import { describe, expect, it } from 'vitest'
import { notificationVariables } from './notification-variables'

describe('notificationVariables', () => {
  it('reads the string and number values of a metadata object', () => {
    expect(notificationVariables('{"tenantName":"Acme","count":3}')).toEqual({ tenantName: 'Acme', count: 3 })
  })

  it('leaves out values that cannot be interpolated', () => {
    expect(notificationVariables('{"tenantName":"Acme","nested":{"a":1},"list":[1],"flag":true,"none":null}')).toEqual({ tenantName: 'Acme' })
  })

  it.each([undefined, null, '', 'not json', '[1,2]', '"text"', '42', 'null'])('yields no variables for %j', (metadata) => {
    expect(notificationVariables(metadata)).toBeUndefined()
  })
})
