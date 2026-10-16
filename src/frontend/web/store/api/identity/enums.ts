/**
 * The scope a permission may be exercised in. Mirrors the API's `PermissionScope`: a session carries
 * only the permissions of the scope it is acting in, so `Tenant` is absent from a platform-scope
 * session, `Platform` is absent from one acting inside a tenant, and `Both` is carried either way.
 */
export enum PermissionScope {
  Tenant = 'Tenant',
  Platform = 'Platform',
  Both = 'Both',
}
