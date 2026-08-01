/** Props for the DataTableSortIcon, the small chevron rendered next to a sortable data-table column header to indicate its current sort state. */
interface DataTableSortIconProps {
  isSorted: boolean | 'asc' | 'desc'
}

/**
 * DataTableSortIcon renders a chevron icon that reflects the current sort state of a data-table column header (up for asc, down for desc, and a faded double-chevron placeholder on hover when not sorted).
 */
export function DataTableSortIcon({ isSorted }: DataTableSortIconProps) {
  if (!isSorted) {
    return (
      <svg width="1em" height="1em" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" className="ml-1 h-5 w-5 opacity-0 group-hover:opacity-50">
        <path d="M8 10L12 6L16 10" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
        <path d="M8 14L12 18L16 14" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
    )
  }

  if (isSorted === 'asc') {
    return (
      <svg width="1em" height="1em" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" className="ml-1 h-5 w-5">
        <path d="M8 10L12 6L16 10" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
    )
  }

  return (
    <svg width="1em" height="1em" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" className="ml-1 h-5 w-5">
      <path d="M8 14L12 18L16 14" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}
