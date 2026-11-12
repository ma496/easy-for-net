import React, { createContext, useContext, useState, ReactNode, useMemo } from 'react'
import { useTable, type ColumnVisibilityState, type PaginationState, type ReactTable, type RowData, type RowSelectionState, type SortingState } from '@tanstack/react-table'
import { dataTableFeatures, type DataTableColumnDef, type DataTableFeatures } from './features'

/** Shape of the data-table context shared by DataTableProvider, exposing the data, columns, sort/pagination/filter/selection state, and the underlying tanstack-react-table instance. */
interface DataTableContextProps<TData extends RowData> {
  data: TData[]
  rowCount?: number
  columns: DataTableColumnDef<TData>[]
  sorting: SortingState
  setSorting: React.Dispatch<React.SetStateAction<SortingState>>
  pagination: PaginationState
  setPagination: React.Dispatch<React.SetStateAction<PaginationState>>
  globalFilter: string
  setGlobalFilter: React.Dispatch<React.SetStateAction<string>>
  columnVisibility: ColumnVisibilityState
  setColumnVisibility: React.Dispatch<React.SetStateAction<ColumnVisibilityState>>
  pageSizeOptions: number[]
  rowSelection: RowSelectionState
  setRowSelection: React.Dispatch<React.SetStateAction<RowSelectionState>>
  enableRowSelection: boolean
  table: ReactTable<DataTableFeatures, TData>
  isFetching?: boolean
}

// The provider is generic over the row type and a context cannot be, so the value is held untyped and useDataTable names the row type back.
const DataTableContext = createContext<unknown>(undefined)

/**
 * useDataTable is the context hook for accessing the shared DataTable state and the underlying tanstack-react-table instance, throwing if used outside a DataTableProvider.
 */
export function useDataTable<TData extends RowData>() {
  const context = useContext(DataTableContext)
  if (context === undefined) {
    throw new Error('useDataTable must be used within a DataTableProvider')
  }
  return context as DataTableContextProps<TData>
}

/** Props for the DataTableProvider, which owns the parent-controlled state (sorting, pagination, global filter) and wires it into a tanstack-react-table instance configured for manual (server-side) operations. */
interface DataTableProviderProps<TData extends RowData> {
  children: ReactNode
  data: TData[]
  rowCount?: number
  columns: DataTableColumnDef<TData>[]
  pageSizeOptions?: number[]
  enableRowSelection?: boolean
  sorting: SortingState
  setSorting: React.Dispatch<React.SetStateAction<SortingState>>
  pagination: PaginationState
  setPagination: React.Dispatch<React.SetStateAction<PaginationState>>
  globalFilter: string
  setGlobalFilter: React.Dispatch<React.SetStateAction<string>>
  isFetching?: boolean
}

/**
 * DataTableProvider is a context provider that holds a tanstack-react-table instance configured for manual (server-side) sorting and pagination, plus column visibility and row selection state, exposing them to descendant data-table subcomponents.
 * The search text is the caller's (`globalFilter`), filtered by the server, so the table registers no filtering feature and the toolbar reads and writes it through this context.
 */
export function DataTableProvider<TData extends RowData>({
  children,
  data,
  rowCount,
  columns,
  pageSizeOptions = [10, 20, 30, 50, 100],
  enableRowSelection = false,
  sorting,
  setSorting,
  pagination,
  setPagination,
  globalFilter,
  setGlobalFilter,
  isFetching,
}: DataTableProviderProps<TData>) {
  const [columnVisibility, setColumnVisibility] = useState<ColumnVisibilityState>({})
  const [rowSelection, setRowSelection] = useState<RowSelectionState>({})
  const table = useTable({
    features: dataTableFeatures,
    data,
    columns,
    rowCount,
    state: {
      sorting,
      pagination,
      columnVisibility,
      rowSelection,
    },
    enableRowSelection,
    onSortingChange: setSorting,
    onPaginationChange: setPagination,
    onColumnVisibilityChange: setColumnVisibility,
    onRowSelectionChange: setRowSelection,
    manualSorting: true,
    manualPagination: true,
  })

  const value = useMemo(
    () => ({
      data,
      rowCount,
      columns,
      sorting,
      setSorting,
      pagination,
      setPagination,
      globalFilter,
      setGlobalFilter,
      columnVisibility,
      setColumnVisibility,
      pageSizeOptions,
      rowSelection,
      setRowSelection,
      enableRowSelection,
      table,
      isFetching,
    }),
    [data, rowCount, columns, sorting, pagination, globalFilter, columnVisibility, pageSizeOptions, rowSelection, enableRowSelection, table, isFetching, setSorting, setPagination, setGlobalFilter],
  )

  return <DataTableContext.Provider value={value}>{children}</DataTableContext.Provider>
}
