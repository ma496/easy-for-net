import { cache } from 'react'
import type { TenantHostGetResponse } from '@/store/api/tenancy'
import { serverApiFetch } from './api-fetch'

/**
 * What the visitor's host names, as a page renders it:
 *
 * - `found` - the API answered `GET /tenants/host`: the platform host (or any host while host
 *   resolution is off) or an existing tenant's;
 * - `unknownHost` - the host is neither the platform's address nor a tenant's (`unknown`), so the page
 *   offers both: the platform, and a tenant's address built from `tenantUrlTemplate`;
 * - `notFound` - the host has a tenant's shape but names no tenant (a 404 `tenantNotFound`: an unknown or
 *   deleted tenant). The 404 carries no `platformUrl`, so it comes from a second call that forwards no
 *   host, which the API answers with the platform origin; it is null when that call fails too;
 * - `unavailable` - the API could not be asked, so the page falls back to its platform copy and keeps
 *   working.
 *
 * `visitorHost` is the host the browser asked for, to name it in copy ("Sign in to ..." under the
 * tenant's name, "There is no tenant at ...").
 */
export type HostTenant =
  | { kind: 'found'; host: TenantHostGetResponse; visitorHost: string | null }
  | { kind: 'unknownHost'; platformUrl: string; tenantUrlTemplate: string | null; visitorHost: string | null }
  | { kind: 'notFound'; platformUrl: string | null; visitorHost: string | null }
  | { kind: 'unavailable'; visitorHost: string | null }

/** Calls `GET /tenants/host`, forwarding the visitor's host or not. */
export type HostFetcher = (options: { forwardHost: boolean }) => Promise<Response>

/** Reads a host answer's body, or null when it is not one. */
const readHost = async (response: Response): Promise<TenantHostGetResponse | null> => {
  try {
    const body = (await response.json()) as Partial<TenantHostGetResponse> | null
    return body && (body.kind === 'platform' || body.kind === 'tenant' || body.kind === 'unknown') && typeof body.platformUrl === 'string' ? (body as TenantHostGetResponse) : null
  } catch {
    return null
  }
}

/**
 * Decides the {@link HostTenant} from the API's answers, through `fetchHost` so the decision is
 * testable without a request. Never throws: anything unexpected is `unavailable`.
 */
export async function resolveHostTenant(fetchHost: HostFetcher, visitorHost: string | null): Promise<HostTenant> {
  try {
    const response = await fetchHost({ forwardHost: true })
    if (response.ok) {
      const host = await readHost(response)
      if (!host) {
        return { kind: 'unavailable', visitorHost }
      }
      return host.kind === 'unknown' ? { kind: 'unknownHost', platformUrl: host.platformUrl, tenantUrlTemplate: host.tenantUrlTemplate ?? null, visitorHost } : { kind: 'found', host, visitorHost }
    }
    if (response.status !== 404) {
      return { kind: 'unavailable', visitorHost }
    }
  } catch {
    return { kind: 'unavailable', visitorHost }
  }

  try {
    const platform = await fetchHost({ forwardHost: false })
    const host = platform.ok ? await readHost(platform) : null
    return { kind: 'notFound', platformUrl: host?.platformUrl ?? null, visitorHost }
  } catch {
    return { kind: 'notFound', platformUrl: null, visitorHost }
  }
}

/**
 * The visitor's {@link HostTenant}, fetched on the server once per request (`cache()`), so a page
 * hands it to its client component as a prop and renders the right heading with no layout shift.
 */
export const getHostTenant = cache(async (): Promise<HostTenant> => {
  const { headers } = await import('next/headers')
  const visitorHost = (await headers()).get('host')

  return resolveHostTenant(({ forwardHost }) => serverApiFetch('/tenants/host', { forwardHost, cache: 'no-store' }), visitorHost)
})
