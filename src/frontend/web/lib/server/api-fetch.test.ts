import { describe, expect, it } from 'vitest'
import { forwardedHostHeaders } from './api-fetch'

describe('forwardedHostHeaders', () => {
  it('forwards the incoming host as X-Forwarded-Host', () => {
    expect(forwardedHostHeaders('acme.example.com')).toEqual({ 'X-Forwarded-Host': 'acme.example.com' })
  })

  it.each(['default.localhost:3000', 'App.Example.com:8443', 'localhost'])('copies the host %s verbatim, port and case included', (host) => {
    expect(forwardedHostHeaders(host)).toEqual({ 'X-Forwarded-Host': host })
  })

  it.each([null, undefined, ''])('sends no header when there is no incoming host (%s)', (host) => {
    expect(forwardedHostHeaders(host)).toEqual({})
  })
})
