import type { Row, RowData } from '@tanstack/react-table'
import type { DataTableFeatures } from './features'

/** Props for the DataTableCheckboxCell, a tanstack-table row-selection checkbox bound to a single table row. */
interface DataTableCheckboxCellProps<TData extends RowData> {
  row: Row<DataTableFeatures, TData>
}

/**
 * DataTableCheckboxCell renders a single row's selection checkbox in a data table by wiring it to the row's `getToggleSelectedHandler` from tanstack-react-table.
 */
export function DataTableCheckboxCell<TData extends RowData>({ row }: DataTableCheckboxCellProps<TData>) {
  return (
    <div className="flex items-center">
      <input
        type="checkbox"
        className="form-checkbox"
        checked={row.getIsSelected()}
        onChange={row.getToggleSelectedHandler()}
      />
    </div>
  )
}

/** Props for the DataTableCheckboxHeader, a "select all" checkbox in the data-table header that supports an indeterminate state. */
interface DataTableCheckboxHeaderProps {
  checked: boolean
  indeterminate: boolean
  onChange: () => void
}

/**
 * DataTableCheckboxHeader renders the header "select-all" checkbox for a data table, exposing a controlled checked/indeterminate state for partial-selection scenarios.
 */
export function DataTableCheckboxHeader({ checked, indeterminate, onChange }: DataTableCheckboxHeaderProps) {
  return (
    <div className="flex items-center">
      <input
        type="checkbox"
        className="form-checkbox"
        checked={checked}
        ref={(el) => {
          if (el) {
            el.indeterminate = indeterminate
          }
        }}
        onChange={onChange}
      />
    </div>
  )
}
