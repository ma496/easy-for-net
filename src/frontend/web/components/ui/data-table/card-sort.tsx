import { useId } from 'react'
import { ArrowDownWideNarrow, ArrowUpNarrowWide } from 'lucide-react'
import { useDataTable } from './context'
import { useTranslation } from '@/i18n'
import { Select } from '../form/select'

/**
 * DataTableCardSort stands in for the clickable column headers the card list has no room for: a
 * themed select of the sortable columns (a native one opens an OS-drawn list on phones) and a button flipping the direction, both bound to the table's
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
      <label htmlFor={selectId} className="shrink-0 text-sm text-muted-foreground">
        {t('table.sort.label')}
      </label>
      <Select
        name="sort"
        id={selectId}
        className="min-w-0 flex-1"
        options={[
          { value: '', label: t('table.sort.none') },
          ...sortable.map((column) => {
            const header = column.columnDef.header
            return { value: column.id, label: typeof header === 'string' ? header : column.id }
          }),
        ]}
        value={current?.id ?? ''}
        onChange={(_, value) => table.setSorting(value ? [{ id: value, desc: descending }] : [])}
        placeholder={t('table.sort.none')}
        searchable={false}
        clearable={false}
        showValidation={false}
      />
      <button
        type="button"
        className="btn btn-secondary size-9 shrink-0 p-0"
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
