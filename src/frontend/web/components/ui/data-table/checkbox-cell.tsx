import { Row } from '@tanstack/react-table'

/** Props for the DataTableCheckboxCell, a tanstack-table row-selection checkbox bound to a single table row. */
interface DataTableCheckboxCellProps<TData> {
  row: Row<TData>
}

/**
 * DataTableCheckboxCell renders a single row's selection checkbox in a data table by wiring it to the row's `getToggleSelectedHandler` from tanstack-react-table.
 */
export function DataTableCheckboxCell<TData>({ row }: DataTableCheckboxCellProps<TData>) {
  return (
    <div className="flex items-center">
      <input
        type="checkbox"
        className="form-checkbox h-5 w-5 cursor-pointer rounded-sm border-2 border-white-light bg-transparent text-primary shadow-none! ring-0! ring-offset-0! outline-hidden! checked:bg-size-[90%_90%] disabled:cursor-not-allowed dark:border-[#253b5c]"
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
        className="form-checkbox h-5 w-5 cursor-pointer rounded-sm border-2 border-white-light bg-transparent text-primary shadow-none! ring-0! ring-offset-0! outline-hidden! checked:bg-size-[90%_90%] disabled:cursor-not-allowed dark:border-[#253b5c]"
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
