import { describe, expect, it } from 'vitest'
import { forwardedHostHeaders, serverApiRequestHeaders } from './api-fetch'

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

describe('serverApiRequestHeaders', () => {
  const incoming = { host: 'acme.localhost:3000', cookie: 'auth=abc' }

  it("forwards the visitor's host by default, and no cookies unless asked", () => {
    expect(serverApiRequestHeaders(incoming)).toEqual({ 'X-Forwarded-Host': 'acme.localhost:3000' })
  })

  it('forwards the cookies when asked', () => {
    expect(serverApiRequestHeaders(incoming, { forwardCookies: true })).toEqual({ 'X-Forwarded-Host': 'acme.localhost:3000', cookie: 'auth=abc' })
  })

  it("sends no host at all with forwardHost: false, so the API resolves none and answers for the platform", () => {
    expect(serverApiRequestHeaders(incoming, { forwardHost: false })).toEqual({})
    expect(serverApiRequestHeaders(incoming, { forwardHost: false, forwardCookies: true })).toEqual({ cookie: 'auth=abc' })
  })

  it("keeps the caller's own headers, but never lets one stand in for the visitor's host", () => {
    expect(serverApiRequestHeaders(incoming, { headers: { 'Accept-Language': 'fr', 'X-Forwarded-Host': 'other.example.com' } })).toEqual({
      'Accept-Language': 'fr',
      'X-Forwarded-Host': 'acme.localhost:3000',
    })
  })

  it('sends no cookie header when the visitor sent none', () => {
    expect(serverApiRequestHeaders({ host: null, cookie: null }, { forwardCookies: true })).toEqual({})
  })
})
