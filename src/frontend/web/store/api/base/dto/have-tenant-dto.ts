/** DTO that reports the tenant its row belongs to, for a kind whose rows always belong to exactly one tenant; the strict counterpart of `MayHaveTenantDto`, with no platform-scoped row to represent. */
export interface HaveTenantDto {
  tenantId: string
}
