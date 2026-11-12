/** Generic paged list response containing a page of items and the total record count. */
export interface ListDto<T> {
  items: T[]
  total: number
}
