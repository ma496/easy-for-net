import { BaseDto, RequestBase, GenericAuditableDto, ListRequestDto, ListDto } from '@/store/api'

/** Request body for creating an edition — a plan the platform can put tenants on. */
export interface EditionCreateRequest extends RequestBase {
  name: string
  description?: string | null
  displayOrder: number
}

/** Response from the create-edition endpoint, returning the assigned id and the plan as stored. */
export interface EditionCreateResponse extends BaseDto<string> {
  name: string
  description?: string | null
  displayOrder: number
}

/** Request parameters for fetching a single edition by id. */
export interface EditionGetRequest extends BaseDto<string>, RequestBase {}

/** Response from the get-edition endpoint, including how many tenants are on the plan. */
export interface EditionGetResponse extends GenericAuditableDto<string> {
  name: string
  description?: string | null
  displayOrder: number
  /** How many tenants are on this plan. A plan anyone is on cannot be deleted. */
  tenantCount: number
}

/** Request parameters for the list-editions endpoint. */
export interface EditionListRequest extends ListRequestDto<string>, RequestBase {}

/** Paged response of editions returned by the list-editions endpoint. */
export interface EditionListResponse extends ListDto<EditionListDto> {}

/** One edition as the list renders it. */
export interface EditionListDto extends GenericAuditableDto<string> {
  name: string
  description?: string | null
  displayOrder: number
  tenantCount: number
}

/** Request body for updating an edition's name, description or ordering. */
export interface EditionUpdateRequest extends BaseDto<string>, RequestBase {
  name: string
  description?: string | null
  displayOrder: number
}

/** Response from the update-edition endpoint, echoing the plan as it now stands. */
export interface EditionUpdateResponse extends BaseDto<string> {
  name: string
  description?: string | null
  displayOrder: number
}

/** Request parameters for soft-deleting an edition by id. */
export interface EditionDeleteRequest extends BaseDto<string>, RequestBase {}

/** Response from the delete-edition endpoint. */
export interface EditionDeleteResponse extends BaseDto<string> {}
