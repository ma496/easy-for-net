import { BaseDto, RequestBase, GenericAuditableDto, ListRequestDto, ListDto } from '@/store/api'

/** Request body for creating a new user, supplying credentials, profile fields, active flag, and assigned role ids. */
export interface UserCreateRequest extends RequestBase {
  username: string
  email: string
  password: string
  firstName?: string
  lastName?: string
  isActive: boolean
  roles: string[]
}

/** Response from the create-user endpoint, returning the new user's id, normalized identifiers, and assigned roles. */
export interface UserCreateResponse extends BaseDto<string> {
  username: string
  usernameNormalized: string
  email: string
  emailNormalized: string
  firstName?: string
  lastName?: string
  isActive: boolean
  roles: string[]
}

/** Request parameters for deleting a user by id. */
export interface UserDeleteRequest extends BaseDto<string>, RequestBase { }

/** Response from the delete-user endpoint, indicating success and an accompanying message. */
export interface UserDeleteResponse extends BaseDto<string> {
  success: boolean
  message: string
}

/** Request parameters for fetching a single user by id. */
export interface UserGetRequest extends BaseDto<string>, RequestBase { }

/** Response from the get-user endpoint, returning the user's profile, status, roles, and audit fields. */
export interface UserGetResponse extends GenericAuditableDto<string> {
  username: string
  usernameNormalized: string
  email: string
  emailNormalized: string
  firstName?: string
  lastName?: string
  isActive: boolean
  roles: string[]
}

/** Request parameters for the list-users endpoint, extending the standard list options with isActive/roleId filters. */
export interface UserListRequest extends ListRequestDto<string>, RequestBase {
  isActive?: boolean
  roleId?: string
}

/** Paged response of users returned by the list-users endpoint. */
export interface UserListResponse extends ListDto<UserListDto> { }

/** Summary representation of a user in list responses, with expanded role objects. */
export interface UserListDto extends GenericAuditableDto<string> {
  username: string
  usernameNormalized: string
  email: string
  emailNormalized: string
  firstName: string
  lastName: string
  isActive: boolean
  roles: UserRoleDto[]
}

/** Minimal role descriptor embedded in user list/detail responses. */
export interface UserRoleDto extends BaseDto<string> {
  name: string
}

/** Request body for updating a user's profile, active state, and assigned roles. */
export interface UserUpdateRequest extends BaseDto<string>, RequestBase {
  firstName?: string
  lastName?: string
  isActive: boolean
  roles: string[]
}

/** Response from the update-user endpoint, returning the persisted name, active state, and roles. */
export interface UserUpdateResponse extends BaseDto<string> {
  firstName?: string
  lastName?: string
  isActive: boolean
  roles: string[]
}
