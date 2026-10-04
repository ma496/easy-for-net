import { flexRender } from '@tanstack/react-table'
import { useDataTable } from './context'
import { DataTableSortIcon } from './sort-icon'
import { Loader } from '..'
import { useTranslation } from '@/i18n'
import ScrollBar from 'react-perfect-scrollbar'
import { useEffect, useRef } from 'react'
import { DataTableCardList } from './card-list'
import { DataTableCardSort } from './card-sort'
import { DataTableEmpty } from './empty-state'

/** Below which breakpoint rows render as cards rather than a table; `false` keeps the table at every width. */
export type DataTableCardsBelow = 'sm' | 'md' | 'lg' | false

// Spelled out in full so Tailwind sees every class it has to generate.
const responsiveClasses: Record<'sm' | 'md' | 'lg', { table: string; cards: string }> = {
  sm: { table: 'hidden sm:block', cards: 'sm:hidden' },
  md: { table: 'hidden md:block', cards: 'md:hidden' },
  lg: { table: 'hidden lg:block', cards: 'lg:hidden' },
}

/** Props for the DataTable, the body component that renders the table's header/rows from the shared DataTable context, with optional scrollbar suppression. */
interface DataTableProps {
  className?: string
  suppressScrollX?: boolean
  suppressScrollY?: boolean
  /** Shown instead of the default "no records" message when there are no rows to render. */
  emptyMessage?: string
  /** Below this breakpoint each row renders as a card (placed by `meta.card` on its columns). Defaults to `md`. */
  cardsBelow?: DataTableCardsBelow
}

/**
 * DataTable is the body component of the data-table system that renders the table's headers (with click-to-sort affordance), rows, and either a loading indicator, a localized "no records" message (or a caller-supplied `emptyMessage`), or the data rows themselves from the shared DataTable context.
 * Below `cardsBelow` it renders the same rows as a card list with a sort control instead, so a phone never scrolls a record sideways.
 */
export function DataTable<TData>({ cardsBelow = 'md', emptyMessage, ...gridProps }: DataTableProps) {
  if (!cardsBelow) {
    return <DataTableGrid<TData> emptyMessage={emptyMessage} {...gridProps} />
  }

  const classes = responsiveClasses[cardsBelow]
  return (
    <>
      <div className={classes.table}>
        <DataTableGrid<TData> emptyMessage={emptyMessage} {...gridProps} />
      </div>
      <div className={classes.cards}>
        <DataTableCardSort<TData> />
        <DataTableCardList<TData> emptyMessage={emptyMessage} />
      </div>
    </>
  )
}

/** The table rendering of DataTable: at every width when cards are off, above `cardsBelow` otherwise. */
function DataTableGrid<TData>({ className = '', suppressScrollX = false, suppressScrollY = true, emptyMessage }: Omit<DataTableProps, 'cardsBelow'>) {
  const { columns, table, isFetching } = useDataTable<TData>()
  const { t } = useTranslation()
  const scrollBarRef = useRef<ScrollBar>(null)
  const containerRef = useRef<HTMLElement | null>(null)

  // perfect-scrollbar measures only when it mounts or re-renders, so a window resize, a sidebar toggle or a table
  // shown from display:none would leave its rails sized for the old width. Re-measure whenever the container or the
  // table changes size, so the horizontal rail appears on hover exactly when the table is wider than its space.
  useEffect(() => {
    const container = containerRef.current
    if (!container || typeof ResizeObserver === 'undefined') return
    const observer = new ResizeObserver(() => scrollBarRef.current?.updateScroll())
    observer.observe(container)
    if (container.firstElementChild) observer.observe(container.firstElementChild)
    return () => observer.disconnect()
  }, [])

  return (
    <ScrollBar
      ref={scrollBarRef}
      containerRef={(element) => {
        containerRef.current = element
      }}
      options={{
        suppressScrollX,
        suppressScrollY,
        // A full-width table can measure a fraction of a pixel wider than its container; that is not overflow.
        scrollXMarginOffset: 1,
        scrollYMarginOffset: 1,
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
                    className="h-10 bg-surface-2 px-4 text-start text-xs font-medium tracking-wide whitespace-nowrap text-muted-foreground uppercase first:rounded-s-lg last:rounded-e-lg"
                    style={{ width: header.getSize(), minWidth: header.column.columnDef.minSize, maxWidth: header.column.columnDef.maxSize }}
                  >
                    {header.isPlaceholder ? null : (
                      <div className={`group flex items-center gap-1 ${header.column.getCanSort() ? 'cursor-pointer select-none hover:text-foreground' : ''}`} onClick={header.column.getToggleSortingHandler()}>
                        {flexRender(header.column.columnDef.header, header.getContext())}
                        {header.column.getCanSort() && <DataTableSortIcon isSorted={header.column.getIsSorted()} />}
                      </div>
                    )}
                  </th>
                ))}
              </tr>
            ))}
          </thead>
          <tbody className={isFetching ? 'pointer-events-none opacity-60 transition-opacity duration-150' : 'transition-opacity duration-150'} aria-busy={isFetching}>
            {/* The spinner stands in only for a first load; a refetch keeps the current rows, dimmed, so the table does not jump. */}
            {isFetching && table.getRowModel().rows.length === 0 ? (
              <tr>
                <td colSpan={columns.length} className="py-14 text-center">
                  <Loader size="lg" />
                </td>
              </tr>
            ) : table.getRowModel().rows.length > 0 ? (
              table.getRowModel().rows.map((row) => (
                <tr
                  key={row.id}
                  className={`border-b border-border transition-colors last:border-b-0 hover:bg-surface-2/60 ${row.getIsSelected() ? 'bg-primary/5' : ''}`}
                >
                  {row.getVisibleCells().map((cell) => (
                    <td
                      key={cell.id}
                      className="px-4 py-3 text-foreground"
                      style={{ width: cell.column.getSize(), minWidth: cell.column.columnDef.minSize, maxWidth: cell.column.columnDef.maxSize }}
                    >
                      {flexRender(cell.column.columnDef.cell, cell.getContext())}
                    </td>
                  ))}
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={columns.length}>
                  <DataTableEmpty message={emptyMessage ?? t('table.noRecords')} />
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </ScrollBar>
  )
}
