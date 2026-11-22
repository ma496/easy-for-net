import { describe, expect, it } from 'vitest'
import { devProxyRedirect } from './dev-proxy-redirect'

const url = (path: string) => new URL(path, 'http://localhost:3001')

describe('devProxyRedirect', () => {
  it('sends a page opened on next dev to the same path and query through the proxy', () => {
    expect(devProxyRedirect(url('/signin?redirect=%2Fadmin'), 'localhost:3001', '3000')?.href).toBe('http://localhost:3000/signin?redirect=%2Fadmin')
  })

  it('keeps the hostname the visitor typed', () => {
    expect(devProxyRedirect(url('/'), 'default.localhost:3001', '3000')?.href).toBe('http://default.localhost:3000/')
    expect(devProxyRedirect(url('/'), '127.0.0.1:3001', '3000')?.href).toBe('http://127.0.0.1:3000/')
    expect(devProxyRedirect(url('/'), '[::1]:3001', '3000')?.href).toBe('http://[::1]:3000/')
  })

  it('leaves a request that came through the proxy alone', () => {
    expect(devProxyRedirect(url('/signin'), 'localhost:3000', '3000')).toBeNull()
    expect(devProxyRedirect(url('/signin'), 'default.localhost:3000', '3000')).toBeNull()
  })

  it('does nothing when no dev proxy fronts the server', () => {
    expect(devProxyRedirect(url('/signin'), 'localhost:3001', undefined)).toBeNull()
    expect(devProxyRedirect(url('/signin'), 'localhost:3001', '')).toBeNull()
  })

  it('does nothing without a usable Host header', () => {
    expect(devProxyRedirect(url('/signin'), null, '3000')).toBeNull()
    expect(devProxyRedirect(url('/signin'), 'bad host:x', '3000')).toBeNull()
  })
})
