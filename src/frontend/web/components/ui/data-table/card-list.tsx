import { Cell, flexRender, Header } from '@tanstack/react-table'
import { useDataTable } from './context'
import { resolveCardLayout } from './card-layout'
import { Loader } from '..'
import { useTranslation } from '@/i18n'
import { cn } from '@/lib/utils'
import './column-meta'

/** Props for DataTableCardList, the small-screen rendering of a data table's rows. */
interface DataTableCardListProps {
  className?: string
  emptyMessage?: string
}

/**
 * DataTableCardList renders each row of the shared DataTable context as a card rather than a table
 * row, so a phone reads a record top to bottom instead of scrolling it sideways. Cells keep the
 * renderers their columns declare; `meta.card` decides where each one sits (see `resolveCardLayout`),
 * and the column header becomes the field's label.
 */
export function DataTableCardList<TData>({ className = '', emptyMessage }: DataTableCardListProps) {
  const { table, isFetching } = useDataTable<TData>()
  const { t } = useTranslation()

  const columns = table.getVisibleLeafColumns()
  const layout = resolveCardLayout(columns.map((column) => ({ id: column.id, placement: column.columnDef.meta?.card })))
  const headers = new Map<string, Header<TData, unknown>>(table.getFlatHeaders().map((header) => [header.column.id, header]))

  const label = (columnId: string) => {
    const header = headers.get(columnId)
    return header && !header.isPlaceholder ? flexRender(header.column.columnDef.header, header.getContext()) : null
  }

  if (isFetching) {
    return (
      <div className="py-6 text-center">
        <Loader size="lg" />
      </div>
    )
  }

  const rows = table.getRowModel().rows
  if (rows.length === 0) {
    return <div className="py-6 text-center">{emptyMessage ?? t('table.noRecords')}</div>
  }

  return (
    <ul className={cn('space-y-3', className)}>
      {rows.map((row) => {
        const cells = new Map<string, Cell<TData, unknown>>(row.getVisibleCells().map((cell) => [cell.column.id, cell]))
        const render = (columnId?: string) => {
          const cell = columnId ? cells.get(columnId) : undefined
          return cell ? flexRender(cell.column.columnDef.cell, cell.getContext()) : null
        }
        // A labelled field with nothing under it reads as broken, so an empty value shows a dash.
        const field = (columnId: string) => {
          const cell = cells.get(columnId)
          const value = cell?.column.accessorFn ? cell.getValue() : undefined
          const empty = cell?.column.accessorFn !== undefined && (value == null || value === '' || (Array.isArray(value) && value.length === 0))
          return empty ? <span className="text-gray-400 dark:text-gray-600">&mdash;</span> : render(columnId)
        }
        const hasBody = layout.fields.length > 0 || layout.wide.length > 0

        return (
          <li
            key={row.id}
            className={cn(
              'rounded-md border border-white-light p-4 dark:border-[#191e3a]',
              row.getIsSelected() ? 'border-primary/40 bg-primary/10 dark:bg-primary/20' : 'bg-white dark:bg-black',
            )}
          >
            <div className="flex items-start gap-3">
              {layout.select && <div className="pt-0.5">{render(layout.select)}</div>}
              <div className="min-w-0 flex-1">
                {layout.title && <div className="font-semibold wrap-break-word text-dark dark:text-white-light">{render(layout.title)}</div>}
                {layout.subtitles.map((id) => (
                  <div key={id} className="mt-0.5 text-xs wrap-break-word text-gray-500 dark:text-gray-400">
                    {render(id)}
                  </div>
                ))}
              </div>
              {(layout.badges.length > 0 || layout.actions) && (
                <div className="flex shrink-0 items-start gap-2">
                  {layout.badges.length > 0 && (
                    <div className="flex flex-col items-end gap-1.5">
                      {layout.badges.map((id) => (
                        <span key={id}>{render(id)}</span>
                      ))}
                    </div>
                  )}
                  {layout.actions && render(layout.actions)}
                </div>
              )}
            </div>

            {hasBody && (
              // One row per field, label at the start and value at the end, so every value lines up
              // whatever the field count; a wide field puts its label above a full-width value.
              <dl className="mt-3 divide-y divide-white-light border-t border-white-light text-sm dark:divide-[#191e3a] dark:border-[#191e3a]">
                {layout.fields.map((id) => (
                  <div key={id} className="flex items-center justify-between gap-4 py-2.5 last:pb-0">
                    <dt className="shrink-0 text-gray-500 dark:text-gray-400">{label(id)}</dt>
                    <dd className="flex min-w-0 justify-end text-end wrap-break-word">{field(id)}</dd>
                  </div>
                ))}
                {layout.wide.map((id) => (
                  <div key={id} className="py-2.5 last:pb-0">
                    <dt className="text-gray-500 dark:text-gray-400">{label(id)}</dt>
                    <dd className="mt-1 wrap-break-word">{field(id)}</dd>
                  </div>
                ))}
              </dl>
            )}
          </li>
        )
      })}
    </ul>
  )
}
