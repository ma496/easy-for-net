import { describe, expect, it, vi } from 'vitest'
import { resolveHostTenant, type HostFetcher } from './host-tenant'

const json = (status: number, body: unknown) => new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } })

const tenantHost = { kind: 'tenant', name: 'Acme', identifier: 'acme', status: 'Active', platformUrl: 'http://app.localhost:3000', tenantUrlTemplate: 'http://{tenant}.localhost:3000' }
const platformHost = { kind: 'platform', platformUrl: 'http://app.localhost:3000', tenantUrlTemplate: 'http://{tenant}.localhost:3000' }
const unknownHost = { kind: 'unknown', platformUrl: 'http://app.localhost:3000', tenantUrlTemplate: 'http://{tenant}.localhost:3000' }
const notFound = { statusCode: 404, errors: [{ name: 'generalErrors', reason: 'Tenant not found', code: 'tenantNotFound' }] }

describe('resolveHostTenant', () => {
  it("answers with what the visitor's host names, asking only once", async () => {
    const fetchHost = vi.fn<HostFetcher>(async () => json(200, tenantHost))

    expect(await resolveHostTenant(fetchHost, 'acme.localhost:3000')).toEqual({ kind: 'found', host: tenantHost, visitorHost: 'acme.localhost:3000' })
    expect(fetchHost).toHaveBeenCalledTimes(1)
    expect(fetchHost).toHaveBeenCalledWith({ forwardHost: true })
  })

  it('takes the platform address for an unknown host from a second call that forwards no host', async () => {
    const fetchHost = vi.fn<HostFetcher>(async ({ forwardHost }) => (forwardHost ? json(404, notFound) : json(200, platformHost)))

    expect(await resolveHostTenant(fetchHost, 'nobody.localhost:3000')).toEqual({ kind: 'notFound', platformUrl: 'http://app.localhost:3000', visitorHost: 'nobody.localhost:3000' })
    expect(fetchHost).toHaveBeenLastCalledWith({ forwardHost: false })
  })

  it("reports a host that is not the platform's nor a tenant's as unknown, with both addresses to offer, asking only once", async () => {
    const fetchHost = vi.fn<HostFetcher>(async () => json(200, unknownHost))

    expect(await resolveHostTenant(fetchHost, 'localhost:3000')).toEqual({
      kind: 'unknownHost',
      platformUrl: 'http://app.localhost:3000',
      tenantUrlTemplate: 'http://{tenant}.localhost:3000',
      visitorHost: 'localhost:3000',
    })
    expect(fetchHost).toHaveBeenCalledTimes(1)
  })

  it('reads the platform address from an unknown answer to the second call, which reaches the API on its own address', async () => {
    const fetchHost = vi.fn<HostFetcher>(async ({ forwardHost }) => (forwardHost ? json(404, notFound) : json(200, unknownHost)))

    expect(await resolveHostTenant(fetchHost, 'nobody.localhost:3000')).toEqual({ kind: 'notFound', platformUrl: 'http://app.localhost:3000', visitorHost: 'nobody.localhost:3000' })
  })

  it('still reports the unknown host when the platform address cannot be read', async () => {
    const failing = vi.fn<HostFetcher>(async ({ forwardHost }) => {
      if (forwardHost) return json(404, notFound)
      throw new Error('down')
    })

    expect(await resolveHostTenant(failing, 'x.localhost:3000')).toEqual({ kind: 'notFound', platformUrl: null, visitorHost: 'x.localhost:3000' })
  })

  it.each([
    ['an unreachable API', async () => Promise.reject(new Error('ECONNREFUSED'))],
    ['a server error', async () => json(500, {})],
    ['a body that is not a host answer', async () => json(200, { hello: 'world' })],
  ])('degrades to unavailable on %s, so the page keeps its platform copy', async (_label, fetcher) => {
    expect(await resolveHostTenant(fetcher as HostFetcher, null)).toEqual({ kind: 'unavailable', visitorHost: null })
  })
})
