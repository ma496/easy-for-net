import type { HostTenant } from '@/lib/server/host-tenant'

/**
 * Whether the sign-up page shows "closed" in place of the form: on any host the API answered for -
 * the platform's or a tenant's - while the platform has sign-up switched off. A tenant host would
 * otherwise point at the main site's sign-up, which is closed too, so it shows "closed" as well; a host
 * naming no tenant, or one that could not be read, keeps its own state - the API is what refuses.
 */
export const isSignupClosed = (hostTenant: HostTenant): boolean => hostTenant.kind === 'found' && hostTenant.host.isSignupEnabled === false
