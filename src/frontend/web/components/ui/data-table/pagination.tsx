import { useDataTable } from './context'
import { useTranslation } from '@/i18n'
import ScrollBar from 'react-perfect-scrollbar'
import { cn } from '@/lib/utils'
import { useId } from 'react'

/** Props for the DataTablePagination, the data-table footer that shows a "showing N-M of T" summary, a page-size selector, and a numbered pagination control. */
interface DataTablePaginationProps {
  className?: string
  siblingCount?: number
}

/**
 * DataTablePagination is the footer component for a data table that renders a "showing entries" summary, a page-size selector, and a numbered pagination control (with first/prev/next/last and ellipsis truncation) wired to the shared DataTable context.
 */
export function DataTablePagination<TData>({ className = '', siblingCount = 1 }: DataTablePaginationProps) {
  const { pageSizeOptions, rowCount, table } = useDataTable<TData>()
  const { t } = useTranslation()

  const pageNumbers = () => {
    const currentPage = table.getState().pagination.pageIndex
    const items = []

    // Number of page buttons to show (not including first/last)
    const maxVisiblePages = siblingCount * 2 + 1

    // For very few pages, show all without ellipsis
    if (table.getPageCount() <= maxVisiblePages + 2) {
      for (let i = 0; i < table.getPageCount(); i++) {
        items.push(
          <button
            key={i}
            onClick={() => table.setPageIndex(i)}
            className={cn('flex h-9 w-9 items-center justify-center rounded-md p-0 font-semibold transition duration-300', {
              'cursor-default bg-primary text-white dark:bg-primary dark:text-white-light': currentPage === i,
              'cursor-pointer border border-white-light bg-white text-dark hover:bg-primary hover:text-white dark:border-[#191e3a] dark:bg-black dark:text-white-light dark:hover:bg-primary':
                currentPage !== i,
            })}
          >
            {i + 1}
          </button>,
        )
      }
      return items
    }

    // Always show first page
    items.push(
      <button
        key="first"
        onClick={() => table.firstPage()}
        className={cn('flex h-9 w-9 items-center justify-center rounded-md p-0 font-semibold transition duration-300', {
          'cursor-default bg-primary text-white dark:bg-primary dark:text-white-light': currentPage === 0,
          'cursor-pointer border border-white-light bg-white text-dark hover:bg-primary hover:text-white dark:border-[#191e3a] dark:bg-black dark:text-white-light dark:hover:bg-primary':
            currentPage !== 0,
        })}
      >
        1
      </button>,
    )

    // Calculate the range of pages to show
    let startPage = Math.max(1, currentPage - siblingCount)
    let endPage = Math.min(table.getPageCount() - 2, currentPage + siblingCount)

    // Adjust when near the beginning
    if (currentPage < siblingCount + 1) {
      endPage = Math.min(startPage + maxVisiblePages - 1, table.getPageCount() - 2)
    }

    // Adjust when near the end
    if (currentPage > table.getPageCount() - (siblingCount + 2)) {
      startPage = Math.max(table.getPageCount() - maxVisiblePages - 1, 1)
    }

    // Show left ellipsis if needed
    if (startPage > 1) {
      items.push(
        <button key="ellipsis-1" className="flex h-9 w-9 items-center justify-center rounded-md p-0" disabled>
          ...
        </button>,
      )
    }

    // Show middle pages
    for (let i = startPage; i <= endPage; i++) {
      items.push(
        <button
          key={i}
          onClick={() => table.setPageIndex(i)}
          className={cn('flex h-9 w-9 items-center justify-center rounded-md p-0 font-semibold transition duration-300', {
            'cursor-default bg-primary text-white dark:bg-primary dark:text-white-light': currentPage === i,
            'cursor-pointer border border-white-light bg-white text-dark hover:bg-primary hover:text-white dark:border-[#191e3a] dark:bg-black dark:text-white-light dark:hover:bg-primary':
              currentPage !== i,
          })}
        >
          {i + 1}
        </button>,
      )
    }

    // Show right ellipsis if needed
    if (endPage < table.getPageCount() - 2) {
      items.push(
        <button key="ellipsis-2" className="flex h-9 w-9 items-center justify-center rounded-md p-0" disabled>
          ...
        </button>,
      )
    }

    // Always show last page
    if (table.getPageCount() > 1) {
      items.push(
        <button
          key="last"
          onClick={() => table.lastPage()}
          className={cn('flex h-9 w-9 items-center justify-center rounded-md p-0 font-semibold transition duration-300', {
            'cursor-default bg-primary text-white dark:bg-primary dark:text-white-light': currentPage === table.getPageCount() - 1,
            'cursor-pointer border border-white-light bg-white text-dark hover:bg-primary hover:text-white dark:border-[#191e3a] dark:bg-black dark:text-white-light dark:hover:bg-primary':
              currentPage !== table.getPageCount() - 1,
          })}
        >
          {table.getPageCount()}
        </button>,
      )
    }

    return items
  }

  // Calculate displayed entry range using the total from context
  const from = rowCount === 0 ? 0 : table.getState().pagination.pageIndex * table.getState().pagination.pageSize + 1
  const to = rowCount === 0 ? 0 : Math.min((table.getState().pagination.pageIndex + 1) * table.getState().pagination.pageSize, rowCount || 0)

  return (
    <div className={cn('mt-5 flex flex-col items-center justify-center gap-4 md:flex-row md:justify-between', className)}>
      <div className="flex items-center justify-center sm:ms-0">
        <div className="flex items-center">
          <span className="whitespace-nowrap">{t('table.pagination.showingEntries', { from, to, totalRecords: rowCount || 0 })}</span>
        </div>
        <div className="flex items-center ms-3">
          <select
            id={useId()}
            className="w-auto min-w-14 appearance-none rounded-md border border-white-light bg-white bg-[url('data:image/svg+xml;charset=utf-8,%3Csvg%20xmlns%3D%22http%3A%2F%2Fwww.w3.org%2F2000%2Fsvg%22%20fill%3D%22none%22%20viewBox%3D%220%200%2020%2020%22%3E%3Cpath%20stroke%3D%22%236b7280%22%20stroke-linecap%3D%22round%22%20stroke-linejoin%3D%22round%22%20stroke-width%3D%221.5%22%20d%3D%22M6%208l4%204%204-4%22%2F%3E%3C%2Fsvg%3E')] bg-size-[1.25rem_1.25rem] bg-no-repeat py-1 text-sm font-semibold text-black cursor-pointer bg-inline-end-[0.2rem] ps-2 pe-7 dark:border-[#17263c] dark:bg-[#121e32] dark:text-white-dark"
            value={table.getState().pagination.pageSize}
            onChange={(e) => table.setPageSize(Number(e.target.value))}
          >
            {pageSizeOptions.map((pageSize) => (
              <option key={pageSize} value={pageSize}>
                {pageSize}
              </option>
            ))}
          </select>
        </div>
      </div>

      <ScrollBar className="w-full sm:w-auto" options={{ suppressScrollX: true }}>
        <div className="flex items-center justify-center gap-2">
          <button
            onClick={() => table.firstPage()}
            disabled={!table.getCanPreviousPage()}
            className={cn(
              'flex h-9 w-9 cursor-pointer items-center justify-center rounded-md border border-white-light bg-white p-0 font-semibold text-dark transition duration-300 dark:border-[#191e3a] dark:bg-black dark:text-white-light',
              {
                'cursor-not-allowed opacity-50': table.getState().pagination.pageIndex === 0,
                'hover:bg-primary hover:text-white dark:hover:bg-primary': table.getState().pagination.pageIndex !== 0,
              },
            )}
            title="First Page"
          >
            <svg className="rtl:scale-x-[-1]" width="16" height="16" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M11 17L6 12L11 7" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
              <path d="M18 17L13 12L18 7" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
          </button>

          <button
            onClick={() => table.previousPage()}
            disabled={!table.getCanPreviousPage()}
            className={cn(
              'flex h-9 w-9 cursor-pointer items-center justify-center rounded-md border border-white-light bg-white p-0 font-semibold text-dark transition duration-300 dark:border-[#191e3a] dark:bg-black dark:text-white-light',
              {
                'cursor-not-allowed opacity-50': table.getState().pagination.pageIndex === 0,
                'hover:bg-primary hover:text-white dark:hover:bg-primary': table.getState().pagination.pageIndex !== 0,
              },
            )}
            title="Previous Page"
          >
            &lt;
          </button>

          {pageNumbers()}

          <button
            onClick={() => table.nextPage()}
            disabled={!table.getCanNextPage()}
            className={cn(
              'flex h-9 w-9 cursor-pointer items-center justify-center rounded-md border border-white-light bg-white p-0 font-semibold text-dark transition duration-300 dark:border-[#191e3a] dark:bg-black dark:text-white-light',
              {
                'cursor-not-allowed opacity-50': table.getState().pagination.pageIndex >= table.getPageCount() - 1,
                'hover:bg-primary hover:text-white dark:hover:bg-primary': table.getState().pagination.pageIndex < table.getPageCount() - 1,
              },
            )}
            title="Next Page"
          >
            &gt;
          </button>

          <button
            onClick={() => table.lastPage()}
            disabled={!table.getCanNextPage()}
            className={cn(
              'flex h-9 w-9 cursor-pointer items-center justify-center rounded-md border border-white-light bg-white p-0 font-semibold text-dark transition duration-300 dark:border-[#191e3a] dark:bg-black dark:text-white-light',
              {
                'cursor-not-allowed opacity-50': table.getState().pagination.pageIndex >= table.getPageCount() - 1,
                'hover:bg-primary hover:text-white dark:hover:bg-primary': table.getState().pagination.pageIndex < table.getPageCount() - 1,
              },
            )}
            title="Last Page"
          >
            <svg className="rtl:scale-x-[-1]" width="16" height="16" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M13 7L18 12L13 17" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
              <path d="M6 7L11 12L6 17" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
          </button>
        </div>
      </ScrollBar>
    </div>
  )
}
