import { SortDirection } from '@/store/api'

/** Standard pagination/sorting/filtering parameters accepted by list endpoints, parameterized by the entity id type. */
export interface ListRequestDto<TId> {
  page?: number
  pageSize?: number
  sortField?: string
  sortDirection?: SortDirection
  search?: string
  all?: boolean
  includeIds?: TId[]
}
