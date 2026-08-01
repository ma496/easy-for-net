import { BaseDto, RequestBase, GenericAuditableDto, ListRequestDto, ListDto } from '@/store/api'

/** Request body for the change-permissions endpoint, listing the new set of permission ids for a role. */
export interface ChangePermissionsRequest extends BaseDto<string>, RequestBase {
  permissions: string[]
}

/** Response from the change-permissions endpoint, echoing the role id and the updated permission list. */
export interface ChangePermissionsResponse extends BaseDto<string> {
  permissions: string[]
}

/** Request body for creating a new role, providing the role's name and optional description. */
export interface RoleCreateRequest extends RequestBase {
  name: string
  description?: string
}

/** Response from the create-role endpoint, returning the new role's id, normalized name, and description. */
export interface RoleCreateResponse extends BaseDto<string> {
  name: string
  nameNormalized: string
  description?: string
}

/** Request body for deleting a role by id. */
export interface RoleDeleteRequest extends BaseDto<string>, RequestBase { }

/** Response from the delete-role endpoint, indicating success and an accompanying message. */
export interface RoleDeleteResponse extends BaseDto<string> {
  success: boolean
  message: string
}

/** Request parameters for fetching a single role by id. */
export interface RoleGetRequest extends BaseDto<string>, RequestBase { }

/** Response from the get-role endpoint, returning the role's name, description, permissions, assigned user count, and audit fields. */
export interface RoleGetResponse extends GenericAuditableDto<string> {
  name: string
  nameNormalized: string
  description: string
  permissions: string[]
  userCount: number
}

/** Request parameters for the list-roles endpoint, combining standard list/search/sort options with the request base. */
export interface RoleListRequest extends ListRequestDto<string>, RequestBase { }

/** Paged response of roles returned by the list-roles endpoint. */
export interface RoleListResponse extends ListDto<RoleListDto> { }

/** Summary representation of a role in list responses, including permission ids and assigned user count. */
export interface RoleListDto extends GenericAuditableDto<string> {
  name: string
  nameNormalized: string
  description: string
  permissions: string[]
  userCount: number
}

/** Request body for updating an existing role's name and description. */
export interface RoleUpdateRequest extends BaseDto<string>, RequestBase {
  name: string
  description?: string
}

/** Response from the update-role endpoint, returning the persisted name/description and the role id. */
export interface RoleUpdateResponse extends BaseDto<string> {
  name: string
  nameNormalized: string
  description?: string
}
