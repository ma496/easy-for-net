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
    <div className="mb-5 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
      {/* Title on left side */}
      {title && <div className="text-xl font-semibold">{title}</div>}

      {/* Right side controls - stack on mobile, row on tablet+ */}
      <div className={cn('flex w-full flex-wrap items-center justify-around gap-4 sm:justify-between', title ? 'sm:w-auto sm:justify-end' : 'sm:justify-between')}>
        {/* Search with icon */}
        <div className="relative">
          <span className="absolute top-1/2 start-3 -translate-y-1/2 text-gray-400">
            <Search size={16} className="text-gray-200 dark:text-gray-400" />
          </span>
          <input
            type="text"
            id={useId()}
            className="form-input w-full max-w-xs rounded-md border-white-light py-2 pe-3 ps-9 text-sm font-semibold text-black placeholder:text-gray-400 focus:border-primary focus:ring-transparent sm:w-auto dark:border-[#17263c] dark:bg-[#121e32] dark:text-white-dark dark:placeholder:text-gray-500 dark:focus:border-primary"
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
