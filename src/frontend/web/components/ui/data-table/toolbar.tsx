import { ReactNode, useId } from 'react'
import { Search } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useTranslation } from '@/i18n'
import { useDataTable } from './context'

/** Props for the DataTableToolbar, the area above a data table that optionally shows a title, a global search input bound to the table's globalFilter, and additional action buttons. */
interface DataTableToolbarProps {
  searchPlaceholder?: string
  title?: string | ReactNode
  children?: React.ReactNode
}

/**
 * DataTableToolbar is the area above a data table that renders an optional title, a search input wired to the table's global filter, and an actions slot for buttons (e.g., "Add new").
 */
export function DataTableToolbar<TData>({ title, children }: DataTableToolbarProps) {
  const { t } = useTranslation()
  const { table } = useDataTable<TData>()

  return (
    <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
      {/* Title on left side */}
      {title && <div className="text-base font-semibold tracking-tight text-foreground">{title}</div>}

      {/* Right side controls - stack on mobile, row on tablet+ */}
      <div className={cn('flex w-full flex-wrap items-center justify-center gap-4 sm:justify-between', title ? 'sm:w-auto sm:justify-end' : 'sm:justify-between')}>
        {/* Search with icon */}
        <div className="relative w-full sm:w-auto">
          <span className="absolute top-1/2 inset-s-3 -translate-y-1/2 text-subtle-foreground">
            <Search size={16} className="text-subtle-foreground" />
          </span>
          <input
            type="text"
            id={useId()}
            className="form-input w-full ps-9 sm:w-72"
            placeholder={t('table.searchPlaceholder')}
            value={table.getState().globalFilter}
            onChange={(e) => table.setGlobalFilter(String(e.target.value))}
          />
        </div>

        {/* Action buttons */}
        {children && <div className="flex items-center gap-2">{children}</div>}
      </div>
    </div>
  )
}
