import { describe, expect, it } from 'vitest'
import type { HostTenant } from '@/lib/server/host-tenant'
import type { TenantHostGetResponse } from '@/store/api/tenancy'
import { isSignupClosed } from './signup-availability'

const found = (overrides: Partial<TenantHostGetResponse> = {}): HostTenant => ({
  kind: 'found',
  visitorHost: 'app.example.com',
  host: { kind: 'platform', platformUrl: 'https://app.example.com', tenantUrlTemplate: 'https://{tenant}.example.com', isSignupEnabled: true, ...overrides },
})

describe('isSignupClosed', () => {
  it('is closed on the platform host while the platform has sign-up off', () => {
    expect(isSignupClosed(found({ isSignupEnabled: false }))).toBe(true)
  })

  it('is open on the platform host while sign-up is on', () => {
    expect(isSignupClosed(found())).toBe(false)
  })

  it('is closed on a tenant host too, rather than pointing at a main-site sign-up that is closed', () => {
    expect(isSignupClosed(found({ kind: 'tenant', name: 'Acme', identifier: 'acme', isSignupEnabled: false }))).toBe(true)
  })

  it('leaves a tenant host to its "sign up on the main site" notice while sign-up is on', () => {
    expect(isSignupClosed(found({ kind: 'tenant', name: 'Acme', identifier: 'acme', isSignupEnabled: true }))).toBe(false)
  })

  it.each<HostTenant>([
    { kind: 'unavailable', visitorHost: null },
    { kind: 'notFound', platformUrl: null, visitorHost: 'gone.example.com' },
    { kind: 'unknownHost', platformUrl: 'https://app.example.com', tenantUrlTemplate: null, visitorHost: 'other.example.com' },
  ])('keeps the form when the host answer is $kind, so the API is what refuses', (hostTenant) => {
    expect(isSignupClosed(hostTenant)).toBe(false)
  })
})
