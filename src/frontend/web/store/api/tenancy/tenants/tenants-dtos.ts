import { TenantStatus } from '../enums'
import { BaseDto, RequestBase, GenericAuditableDto, ListRequestDto, ListDto, SystemCreatedDto, HaveTenantDto } from '@/store/api'

/** Request body for creating a tenant, supplying its display name and its URL-safe identifier. */
export interface TenantCreateRequest extends RequestBase {
  name: string
  identifier: string
  /** The plan to put the tenant on, or null/undefined for none. */
  editionId?: string | null
}

/** Response from the create-tenant endpoint, returning the assigned id, the stored name and identifier, the normalized identifier and the lifecycle status the tenant was persisted in. */
export interface TenantCreateResponse extends BaseDto<string>, SystemCreatedDto {
  name: string
  identifier: string
  identifierNormalized: string
  status: TenantStatus
}

/** Request parameters for soft-deleting a tenant by id. */
export interface TenantDeleteRequest extends BaseDto<string>, RequestBase {}

/** Response from the delete-tenant endpoint, indicating success and an accompanying message. */
export interface TenantDeleteResponse extends BaseDto<string> {
  success: boolean
  message: string
}

/** Request parameters for fetching a single tenant by id. */
export interface TenantGetRequest extends BaseDto<string>, RequestBase {}

/** Response from the get-tenant endpoint, returning the tenant's name, identifiers, lifecycle status and audit fields. */
export interface TenantGetResponse extends GenericAuditableDto<string>, SystemCreatedDto {
  name: string
  identifier: string
  identifierNormalized: string
  status: TenantStatus
  /** The plan the tenant is on, or null when it is on none. */
  editionId?: string | null
  /** What that plan is called, so the screen can name it without a second request. */
  editionName?: string | null
}

/** Request parameters for the list-tenants endpoint, extending the standard list options with a lifecycle-status filter. */
export interface TenantListRequest extends ListRequestDto<string>, RequestBase {
  status?: TenantStatus
}

/** Paged response of tenants returned by the list-tenants endpoint. */
export interface TenantListResponse extends ListDto<TenantListDto> {}

/** Summary representation of a tenant in list responses, carrying its identifiers, lifecycle status, the number of accounts belonging to it and audit fields. */
export interface TenantListDto extends GenericAuditableDto<string>, SystemCreatedDto {
  name: string
  identifier: string
  identifierNormalized: string
  status: TenantStatus
  userCount: number
  /** The plan the tenant is on, or null when it is on none. */
  editionId?: string | null
  /** What that plan is called, so the list can name it without a lookup per row. */
  editionName?: string | null
}

/** Request body for adding an existing user account to a tenant with exactly the supplied tenant roles; the tenant id travels as a path segment. */
export interface TenantMemberAddRequest extends RequestBase {
  tenantId: string
  userId: string
  roles: string[]
}

/** Response from the add-member endpoint, returning the new membership's id together with the tenant, the account and the roles the member actually holds. */
export interface TenantMemberAddResponse extends BaseDto<string>, HaveTenantDto {
  userId: string
  roles: string[]
}

/** Request parameters for the list-tenant-members endpoint, extending the standard list options with the tenant whose members are listed - `tenantId` travels as a path segment, not a query parameter - and an optional role filter. */
export interface TenantMemberListRequest extends ListRequestDto<string>, RequestBase {
  tenantId: string
  roleId?: string
}

/** Request for the tenant-member-seats endpoint; `tenantId` travels as a path segment. */
export interface TenantMemberSeatsRequest {
  tenantId: string
}

/** How many seats a tenant has taken, and how many its plan allows (`null` when no limit applies). */
export interface TenantMemberSeatsResponse {
  used: number
  limit: number | null
}

/** Paged response of tenant members returned by the list-tenant-members endpoint. */
export interface TenantMemberListResponse extends ListDto<TenantMemberListDto> {}

/** Summary representation of one member of a tenant; `id` is the member's user account id, while the roles and audit fields describe that membership alone. */
export interface TenantMemberListDto extends GenericAuditableDto<string> {
  username: string
  email: string
  firstName?: string
  lastName?: string
  isActive: boolean
  memberSince: string
  roles: TenantMemberRoleDto[]
}

/** Minimal role descriptor embedded in tenant member rows, listing only roles held inside the tenant being listed. */
export interface TenantMemberRoleDto extends BaseDto<string> {
  name: string
}

/** Request parameters for removing a member from a tenant; both identities travel as path segments. */
export interface TenantMemberRemoveRequest extends RequestBase {
  tenantId: string
  userId: string
}

/** Response from the remove-member endpoint, indicating success and an accompanying message. */
export interface TenantMemberRemoveResponse extends BaseDto<string> {
  success: boolean
  message: string
}

/** Request body replacing a member's role assignments in a tenant with exactly the supplied roles; both identities travel as path segments. */
export interface TenantMemberUpdateRolesRequest extends RequestBase {
  tenantId: string
  userId: string
  roles: string[]
}

/** Response from the update-member-roles endpoint, echoing the membership and the roles that took effect. */
export interface TenantMemberUpdateRolesResponse extends BaseDto<string>, HaveTenantDto {
  userId: string
  roles: string[]
}

/** Request parameters for reactivating a suspended tenant by id. */
export interface TenantReactivateRequest extends BaseDto<string>, RequestBase {}

/** Response from the reactivate-tenant endpoint, reporting the tenant's lifecycle status after the change. */
export interface TenantReactivateResponse extends BaseDto<string> {
  status: TenantStatus
}

/** Request parameters for suspending an active tenant by id. */
export interface TenantSuspendRequest extends BaseDto<string>, RequestBase {}

/** Response from the suspend-tenant endpoint, reporting the tenant's lifecycle status after the change. */
export interface TenantSuspendResponse extends BaseDto<string> {
  status: TenantStatus
}

/** Request body for renaming a tenant or changing its identifier. */
export interface TenantUpdateRequest extends BaseDto<string>, RequestBase {
  name: string
  identifier: string
  /** The plan to put the tenant on, or null to take it off one. */
  editionId?: string | null
}

/** Response from the update-tenant endpoint, echoing the tenant as it now stands including the normalized identifier. */
export interface TenantUpdateResponse extends BaseDto<string>, SystemCreatedDto {
  name: string
  identifier: string
  identifierNormalized: string
  status: TenantStatus
}

/** What the request's host names, as `GET /tenants/host` answers it: `platform` for the platform host (or any host while host resolution is off), `tenant` for a tenant's own host. */
export type TenantHostKind = 'platform' | 'tenant'

/** Response from the anonymous host endpoint: which workspace the visitor's host belongs to, and the addresses every workspace link is built from. */
export interface TenantHostGetResponse {
  kind: TenantHostKind
  /** The tenant's display name, on a tenant host only. */
  name?: string | null
  /** The tenant's url-safe identifier, on a tenant host only. */
  identifier?: string | null
  /** The tenant's lifecycle status, on a tenant host only. */
  status?: TenantStatus | null
  /** The platform origin. */
  platformUrl: string
  /** The tenant origin template whose first host label is `{tenant}`; null when host resolution is off. */
  tenantUrlTemplate?: string | null
}
