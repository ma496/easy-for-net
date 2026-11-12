'use client'

import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  useQueryStates,
  parseAsInteger,
  parseAsString,
  parseAsStringEnum,
  type UseQueryStatesKeysMap,
} from 'nuqs'
import type { PaginationState, SortingState } from '@tanstack/react-table'

const SEARCH_DEBOUNCE_MS = 500

const tableStateParsers = {
  page: parseAsInteger.withDefault(1).withOptions({ clearOnDefault: true, history: 'push' }),
  size: parseAsInteger.withDefault(10).withOptions({ clearOnDefault: true, history: 'push' }),
  search: parseAsString.withDefault('').withOptions({ clearOnDefault: true, history: 'push' }),
  sort: parseAsString.withOptions({ clearOnDefault: true, history: 'push' }),
  dir: parseAsStringEnum(['asc', 'desc'] as const).withOptions({ clearOnDefault: true, history: 'push' }),
} satisfies UseQueryStatesKeysMap

/**
 * Single hook that syncs a table's full URL state with the URL.
 *
 * Two concerns are covered:
 *
 * 1. **Filter panel state** — generic, per-call-site. Pass any nuqs parsers
 *    via `options.filters`; their values are exposed under `filters.<key>`
 *    with type-safe setters (`filters.setMany`, `filters.clearFilters`).
 * 2. **Table-level state** — fixed: `page`, `size`, `search`, `sort`, `dir`.
 *    These are exposed at the top level of the return value, with TanStack
 *    Table adapters (`pagination`, `sorting`, `setPagination`, `setSorting`,
 *    `setGlobalFilter`) ready to be passed to `DataTableProvider`.
 *
 * Apply `withOptions({ clearOnDefault: true })` to each filter parser (or
 * set defaults globally on `<NuqsAdapter>`) for consistent URL behavior.
 *
 * @example
 *   const { filters, page, pagination, sorting, ... } = useTableUrlState({
 *     filters: {
 *       isActive: parseAsStringEnum(['true', 'false'] as const),
 *       roleId: parseAsString,
 *     },
 *   })
 *   filters.isActive         // 'true' | 'false' | null
 *   filters.roleId           // string | null
 *   filters.setMany({ isActive: 'true', roleId: null })
 *   filters.clearFilters()
 *
 * @example
 *   // Use 'replace' history to avoid polluting the back stack
 *   // when filters or pagination are toggled rapidly.
 *   useTableUrlState({ filters: { ... } }, { history: 'replace' })
 */
export function useTableUrlState<
  F extends UseQueryStatesKeysMap = Record<never, never>
>(options?: { filters?: F; history?: 'push' | 'replace' }) {
  const { filters: filterParsers = {} as F, history = 'push' } = options ?? {}

  const [tableValues, setTableMany] = useQueryStates(tableStateParsers, { history })
  const [filterValues, setFilterMany] = useQueryStates(filterParsers, { history })

  const clearFilters = useCallback(() => {
    // Every nuqs parser accepts `null` as the "clear" value, so an object
    // where each key maps to `null` satisfies the `setFilterMany` contract.
    const nulls = Object.fromEntries(
      Object.keys(filterParsers).map((key) => [key, null])
    ) as { [K in keyof F]: null }
    void setFilterMany(nulls)
  }, [setFilterMany, filterParsers])

  // ---- Search input handling (mirrored locally for snappy typing UX) ----
  const { page, size, search, sort, dir } = tableValues
  const [searchInput, setSearchInputState] = useState(search)
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  // Track the last value pushed to the URL so we don't trigger no-op writes.
  const lastPushedSearchRef = useRef(search)

  // When the URL changes externally (browser back/forward), sync the
  // local input state. The URL is the source of truth; the local state
  // mirrors it for snappy typing UX.
  useEffect(() => {
    if (search !== lastPushedSearchRef.current) {
      setSearchInputState(search)
      lastPushedSearchRef.current = search
    }
  }, [search])

  const writeSearch = useCallback(
    (value: string) => {
      if (value === lastPushedSearchRef.current) return
      lastPushedSearchRef.current = value
      void setTableMany(
        value === ''
          ? { search: null, page: 1 }
          : { search: value, page: 1 }
      )
    },
    [setTableMany]
  )

  const setSearchInput = useCallback(
    (value: string) => {
      setSearchInputState(value)
      if (debounceRef.current) {
        clearTimeout(debounceRef.current)
        debounceRef.current = null
      }
      debounceRef.current = setTimeout(() => {
        writeSearch(value)
        debounceRef.current = null
      }, SEARCH_DEBOUNCE_MS)
    },
    [writeSearch]
  )

  const flushSearch = useCallback(() => {
    if (debounceRef.current) {
      clearTimeout(debounceRef.current)
      debounceRef.current = null
    }
    writeSearch(searchInput)
  }, [searchInput, writeSearch])

  // Cleanup the pending timer on unmount.
  useEffect(() => {
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current)
    }
  }, [])

  const resetPage = useCallback(() => {
    void setTableMany({ page: 1 })
  }, [setTableMany])

  // ---- TanStack-Table derived shapes ----
  const pagination = useMemo<PaginationState>(
    () => ({ pageIndex: page - 1, pageSize: size }),
    [page, size]
  )
  const sorting = useMemo<SortingState>(
    () => (sort ? [{ id: sort, desc: dir === 'desc' }] : []),
    [sort, dir]
  )

  // ---- Setter adapters matching TanStack's Dispatch<SetStateAction<...>> ----
  const setPagination: React.Dispatch<React.SetStateAction<PaginationState>> = useCallback(
    (updater) => {
      const next = typeof updater === 'function' ? updater(pagination) : updater
      // pageSize change should reset to page 1; explicit page changes preserve size.
      const pageReset = next.pageSize !== size
      void setTableMany({
        page: pageReset ? 1 : next.pageIndex + 1,
        size: next.pageSize,
      })
    },
    [pagination, size, setTableMany]
  )

  const setSorting: React.Dispatch<React.SetStateAction<SortingState>> = useCallback(
    (updater) => {
      const next = typeof updater === 'function' ? updater(sorting) : updater
      const first = next[0]
      void setTableMany({
        sort: first?.id ?? null,
        dir: first?.desc ? 'desc' : first ? 'asc' : null,
      })
    },
    [sorting, setTableMany]
  )

  const setGlobalFilter: React.Dispatch<React.SetStateAction<string>> = useCallback(
    (updater) => {
      const next = typeof updater === 'function' ? updater(search) : updater
      setSearchInput(next)
    },
    [search, setSearchInput]
  )

  return {
    filters: {
      ...filterValues,
      setMany: setFilterMany,
      clearFilters,
    },
    page,
    pageSize: size,
    search,
    sortField: sort,
    sortDirection: dir,
    pagination,
    sorting,
    setPagination,
    setSorting,
    setGlobalFilter,
    searchInput,
    setSearchInput,
    flushSearch,
    resetPage,
  }
}
