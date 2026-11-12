/** DTO that reports the tenant its row belongs to, for a kind whose rows belong to at most one tenant; a null `tenantId` is platform scope - a row that is not tenant data. */
export interface MayHaveTenantDto {
  tenantId: string | null
}
