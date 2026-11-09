import {
  columnSizingFeature,
  columnVisibilityFeature,
  createColumnHelper,
  metaHelper,
  rowPaginationFeature,
  rowSelectionFeature,
  rowSortingFeature,
  tableFeatures,
  type ColumnDef,
  type RowData,
} from '@tanstack/react-table'
import type { DataTableColumnMeta } from './column-meta'

/**
 * The table features every data table registers. Sorting and paging are server-side (the provider
 * sets them manual), so no client row model is registered; sizing backs the header and cell widths,
 * visibility the visible cells and columns, and selection the opt-in row checkboxes.
 */
export const dataTableFeatures = tableFeatures({
  columnVisibilityFeature,
  columnSizingFeature,
  rowSortingFeature,
  rowPaginationFeature,
  rowSelectionFeature,
  columnMeta: metaHelper<DataTableColumnMeta>(),
})

/** The feature set of every data table, for the types that have to name it. */
export type DataTableFeatures = typeof dataTableFeatures

/** A column of a data table over `TData`, whatever the type of the value it reads. */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export type DataTableColumnDef<TData extends RowData> = ColumnDef<DataTableFeatures, TData, any>

/** A column helper bound to the data-table features; wrap the list in `columns([...])` to keep each column's value type. */
export function createDataTableColumnHelper<TData extends RowData>() {
  return createColumnHelper<DataTableFeatures, TData>()
}
