import { flexRender } from '@tanstack/react-table'
import { useDataTable } from './context'
import { DataTableSortIcon } from './sort-icon'
import { Loading } from '../loading'
import { useTranslation } from '@/i18n'
import ScrollBar from 'react-perfect-scrollbar'

/** Props for the DataTable, the body component that renders the table's header/rows from the shared DataTable context, with optional scrollbar suppression. */
interface DataTableProps {
  className?: string
  suppressScrollX?: boolean
  suppressScrollY?: boolean
}

/**
 * DataTable is the body component of the data-table system that renders the table's headers (with click-to-sort affordance), rows, and either a loading indicator, a localized "no records" message, or the data rows themselves from the shared DataTable context.
 */
export function DataTable<TData>({ className = '', suppressScrollX = false, suppressScrollY = true }: DataTableProps) {
  const { columns, table, isFetching } = useDataTable<TData>()
  const { t } = useTranslation()

  return (
    <ScrollBar
      options={{
        suppressScrollX,
        suppressScrollY,
      }}
    >
      <div className="relative">
        <table className={`w-full table-auto ${className}`}>
          <thead>
            {table.getHeaderGroups().map((headerGroup) => (
              <tr key={headerGroup.id}>
                {headerGroup.headers.map((header) => (
                  <th
                    key={header.id}
                    className="p-4 text-left font-semibold first:rounded-tl-md last:rounded-tr-md"
                    style={{ width: header.getSize(), minWidth: header.column.columnDef.minSize, maxWidth: header.column.columnDef.maxSize }}
                  >
                    {header.isPlaceholder ? null : (
                      <div className={`group flex items-center ${header.column.getCanSort() ? 'cursor-pointer select-none' : ''}`} onClick={header.column.getToggleSortingHandler()}>
                        {flexRender(header.column.columnDef.header, header.getContext())}
                        {header.column.getCanSort() && <DataTableSortIcon isSorted={header.column.getIsSorted()} />}
                      </div>
                    )}
                  </th>
                ))}
              </tr>
            ))}
          </thead>
          <tbody>
            {isFetching ? (
              <tr>
                <td colSpan={columns.length} className="py-6 text-center">
                  <Loading size="lg" />
                </td>
              </tr>
            ) : table.getRowModel().rows.length > 0 ? (
              table.getRowModel().rows.map((row) => (
                <tr
                  key={row.id}
                  className={`border-b border-white-light/40 hover:bg-white-light/20 dark:border-[#191e3a] dark:hover:bg-[#1a2941]/40 ${row.getIsSelected() ? 'bg-primary/10 dark:bg-primary/20' : ''}`}
                >
                  {row.getVisibleCells().map((cell) => (
                    <td
                      key={cell.id}
                      className="px-4 py-3"
                      style={{ width: cell.column.getSize(), minWidth: cell.column.columnDef.minSize, maxWidth: cell.column.columnDef.maxSize }}
                    >
                      {flexRender(cell.column.columnDef.cell, cell.getContext())}
                    </td>
                  ))}
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={columns.length} className="py-6 text-center">
                  {t('table.noRecords')}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </ScrollBar>
  )
}
