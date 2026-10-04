import { useId } from 'react'
import { ArrowDownWideNarrow, ArrowUpNarrowWide } from 'lucide-react'
import { useDataTable } from './context'
import { useTranslation } from '@/i18n'

/**
 * DataTableCardSort stands in for the clickable column headers the card list has no room for: a
 * select of the sortable columns and a button flipping the direction, both bound to the table's
 * sorting state. It renders nothing when no column can be sorted.
 */
export function DataTableCardSort<TData>() {
  const { table } = useDataTable<TData>()
  const { t } = useTranslation()
  const selectId = useId()

  const sortable = table.getVisibleLeafColumns().filter((column) => column.getCanSort())
  if (sortable.length === 0) return null

  const current = table.getState().sorting[0]
  const descending = current?.desc ?? false

  return (
    <div className="mb-3 flex items-center gap-2">
      <label htmlFor={selectId} className="shrink-0 text-sm text-gray-500 dark:text-gray-400">
        {t('table.sort.label')}
      </label>
      <select
        id={selectId}
        className="form-select min-w-0 flex-1 rounded-md border-white-light py-2 text-sm font-semibold text-black focus:border-primary focus:ring-transparent dark:border-[#17263c] dark:bg-[#121e32] dark:text-white-dark dark:focus:border-primary"
        value={current?.id ?? ''}
        onChange={(e) => table.setSorting(e.target.value ? [{ id: e.target.value, desc: descending }] : [])}
      >
        <option value="">{t('table.sort.none')}</option>
        {sortable.map((column) => {
          const header = column.columnDef.header
          return (
            <option key={column.id} value={column.id}>
              {typeof header === 'string' ? header : column.id}
            </option>
          )
        })}
      </select>
      <button
        type="button"
        className="btn btn-outline-secondary btn-sm shrink-0 p-1.5! shadow-none disabled:cursor-not-allowed disabled:opacity-50"
        disabled={!current}
        onClick={() => current && table.setSorting([{ id: current.id, desc: !descending }])}
        aria-label={descending ? t('table.sort.descending') : t('table.sort.ascending')}
        title={descending ? t('table.sort.descending') : t('table.sort.ascending')}
      >
        {descending ? <ArrowDownWideNarrow className="h-4 w-4" /> : <ArrowUpNarrowWide className="h-4 w-4" />}
      </button>
    </div>
  )
}
