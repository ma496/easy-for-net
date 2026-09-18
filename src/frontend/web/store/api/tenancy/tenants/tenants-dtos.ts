import { TenantStatus } from '../enums'
import { BaseDto, RequestBase, GenericAuditableDto, ListRequestDto, ListDto } from '@/store/api'

/** Request body for creating a tenant, supplying its display name and its URL-safe identifier. */
export interface TenantCreateRequest extends RequestBase {
  name: string
  identifier: string
}

/** Response from the create-tenant endpoint, returning the assigned id, the stored name and identifier, the normalized identifier and the lifecycle status the tenant was persisted in. */
export interface TenantCreateResponse extends BaseDto<string> {
  systemCreated: boolean
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
export interface TenantGetResponse extends GenericAuditableDto<string> {
  systemCreated: boolean
  name: string
  identifier: string
  identifierNormalized: string
  status: TenantStatus
}

/** Request parameters for the list-tenants endpoint, extending the standard list options with a lifecycle-status filter. */
export interface TenantListRequest extends ListRequestDto<string>, RequestBase {
  status?: TenantStatus
}

/** Paged response of tenants returned by the list-tenants endpoint. */
export interface TenantListResponse extends ListDto<TenantListDto> {}

/** Summary representation of a tenant in list responses, carrying its identifiers, lifecycle status and audit fields. */
export interface TenantListDto extends GenericAuditableDto<string> {
  systemCreated: boolean
  name: string
  identifier: string
  identifierNormalized: string
  status: TenantStatus
}

/** Request body for adding an existing user account to a tenant with exactly the supplied tenant roles; the tenant id travels as a path segment. */
export interface TenantMemberAddRequest extends RequestBase {
  tenantId: string
  userId: string
  roles: string[]
}

/** Response from the add-member endpoint, returning the new membership's id together with the tenant, the account and the roles the member actually holds. */
export interface TenantMemberAddResponse extends BaseDto<string> {
  tenantId: string
  userId: string
  roles: string[]
}

/** Request parameters for the list-tenant-members endpoint, extending the standard list options with the tenant whose members are listed - `tenantId` travels as a path segment, not a query parameter - and an optional role filter. */
export interface TenantMemberListRequest extends ListRequestDto<string>, RequestBase {
  tenantId: string
  roleId?: string
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
export interface TenantMemberUpdateRolesResponse extends BaseDto<string> {
  tenantId: string
  userId: string
  roles: string[]
}

/** Request body for self-service onboarding, supplying the display name and identifier of the tenant to create. */
export interface TenantOnboardRequest extends RequestBase {
  name: string
  identifier: string
}

/** Response from the onboarding endpoint, returning the new tenant and the re-established session bound to it. */
export interface TenantOnboardResponse extends BaseDto<string> {
  name: string
  identifier: string
  identifierNormalized: string
  status: TenantStatus
  session: TenantSessionDto
}

/** Request parameters for reactivating a suspended tenant by id. */
export interface TenantReactivateRequest extends BaseDto<string>, RequestBase {}

/** Response from the reactivate-tenant endpoint, reporting the tenant's lifecycle status after the change. */
export interface TenantReactivateResponse extends BaseDto<string> {
  status: TenantStatus
}

/** Session material returned when a tenant is switched to or established by onboarding, so a token client can carry on without re-entering credentials. */
export interface TenantSessionDto {
  userId: string
  accessToken: string
  accessTokenExpiry: string
  refreshToken: string
  refreshTokenExpiry: string
}

/** Response from the exit-tenant endpoint, carrying the session material for a session that now acts in no tenant. */
export interface TenantExitResponse {
  session: TenantSessionDto
}

/** Request parameters for suspending an active tenant by id. */
export interface TenantSuspendRequest extends BaseDto<string>, RequestBase {}

/** Response from the suspend-tenant endpoint, reporting the tenant's lifecycle status after the change. */
export interface TenantSuspendResponse extends BaseDto<string> {
  status: TenantStatus
}

/** Request body selecting the tenant that subsequent requests act in. */
export interface TenantSwitchRequest extends RequestBase {
  tenantId: string
}

/** Response from the switch-tenant endpoint, describing the tenant now active and the session that carries it. */
export interface TenantSwitchResponse {
  tenantId: string
  name: string
  identifier: string
  status: TenantStatus
  session: TenantSessionDto
}

/** Request body for renaming a tenant or changing its identifier. */
export interface TenantUpdateRequest extends BaseDto<string>, RequestBase {
  name: string
  identifier: string
}

/** Response from the update-tenant endpoint, echoing the tenant as it now stands including the normalized identifier. */
export interface TenantUpdateResponse extends BaseDto<string> {
  systemCreated: boolean
  name: string
  identifier: string
  identifierNormalized: string
  status: TenantStatus
}
