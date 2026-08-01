'use client'
import { useEffect, useState } from 'react'
import { useUserListQuery, useLazyUserListQuery, useUserDeleteMutation } from '@/store/api/identity/users/users-api'
import { SortDirection } from '@/store/api/base/sort-direction'
import { UserListDto, UserRoleDto } from '@/store/api/identity/users/users-dtos'
import { Download, Loader2, Trash2, Plus, Pencil } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { ExportFormat, successToast, exportData, isAllowed, apiErrorAlert, confirmDeleteAlert, errorAlert } from '@/lib/utils'
import { Dropdown, LocalizedLink, ApiErrorMessages, Badge } from '@/components/ui'
import { useAppSelector } from '@/store/hooks'
import { Allow } from '@/allow'
import { createColumnHelper, ColumnDef } from '@tanstack/react-table'
import { DataTableProvider, DataTableToolbar, DataTablePagination, DataTable } from '@/components/ui/data-table'
import { UserFilterPanel, UserFilters } from './user-filter-panel'
import { UserFilterButton } from './user-filter-button'
import { useTableUrlState } from '@/hooks/use-table-url-state'
import { parseAsString, parseAsStringEnum } from 'nuqs'

/**
 * Interactive client-side data table that lists users with sorting, pagination, search, role/active filters synced to the URL, and permission-gated create/update/delete actions.
 * Also supports exporting the current page or full filtered set to Excel or CSV.
 */
export const UserTable = () => {
  const url = useTableUrlState({
    filters: {
      isActive: parseAsStringEnum(['true', 'false'] as const).withOptions({
        clearOnDefault: true,
        history: 'push',
      }),
      roleId: parseAsString.withOptions({
        clearOnDefault: true,
        history: 'push',
      }),
    },
  })

  const [isExporting, setIsExporting] = useState(false)
  const [filtersOpen, setFiltersOpen] = useState(false)
  const [pendingFilters, setPendingFilters] = useState<UserFilters>({
    isActive: url.filters.isActive ?? '',
    roleId: url.filters.roleId ?? '',
  })
  const { t } = useTranslation()

  const isRTL = useAppSelector((state) => state.theme.rtlClass) === 'rtl'

  const getIsActiveValue = (value: string): boolean | undefined => {
    if (value === 'true') return true
    if (value === 'false') return false
    return undefined
  }

  // When the filter panel opens, sync the draft values from the URL so the
  // user sees the currently-applied filters.
  useEffect(() => {
    if (filtersOpen) {
      setPendingFilters({
        isActive: url.filters.isActive ?? '',
        roleId: url.filters.roleId ?? '',
      })
    }
  }, [filtersOpen, url.filters.isActive, url.filters.roleId])

  // Derived "applied" filters for the active-count badge.
  const appliedFilters: UserFilters = {
    isActive: url.filters.isActive ?? '',
    roleId: url.filters.roleId ?? '',
  }
  const activeFiltersCount = [appliedFilters.isActive, appliedFilters.roleId].filter(Boolean).length

  const {
    data: userListResponse,
    isFetching: isGettingUsers,
    error: getUsersApiError,
  } = useUserListQuery({
    page: url.page,
    pageSize: url.pageSize,
    sortField: url.sortField ?? undefined,
    sortDirection: url.sortDirection === 'desc' ? SortDirection.Desc : SortDirection.Asc,
    search: url.search || undefined,
    isActive: getIsActiveValue(appliedFilters.isActive),
    roleId: appliedFilters.roleId || undefined,
  })

  const [fetchUsers] = useLazyUserListQuery()
  const [deleteUser, { isLoading: isDeletingUser }] = useUserDeleteMutation()

  const authState = useAppSelector((state) => state.auth)
  const canCreate = isAllowed(authState, [Allow.User_Create])
  const canUpdate = isAllowed(authState, [Allow.User_Update])
  const canDelete = isAllowed(authState, [Allow.User_Delete])

  const handleSearch = () => {
    url.filters.setMany({
      isActive: pendingFilters.isActive === '' ? null : (pendingFilters.isActive as 'true' | 'false'),
      roleId: pendingFilters.roleId === '' ? null : pendingFilters.roleId,
    })
    url.resetPage()
  }

  const handleClear = () => {
    const clearedFilters = { isActive: '', roleId: '' }
    setPendingFilters(clearedFilters)
    url.filters.clearFilters()
    url.resetPage()
  }

  const handleFilterChange = (newFilters: UserFilters) => {
    setPendingFilters(newFilters)
  }

  const handleExport = async (format: ExportFormat, all: boolean) => {
    setIsExporting(true)
    try {
      let dataToExport: UserListDto[] = []
      if (all) {
        const response = await fetchUsers({
          page: url.page,
          pageSize: url.pageSize,
          sortField: url.sortField ?? undefined,
          sortDirection: url.sortDirection === 'desc' ? SortDirection.Desc : SortDirection.Asc,
          search: url.search || undefined,
          all: true,
          isActive: getIsActiveValue(appliedFilters.isActive),
          roleId: appliedFilters.roleId || undefined,
        })
        if (response.data) {
          dataToExport = response.data?.items
        }
      } else {
        dataToExport = userListResponse?.items || []
      }

      if (dataToExport.length === 0) return

      const rows = dataToExport.map((user) => ({
        Username: user.username,
        Email: user.email,
        'First Name': user.firstName,
        'Last Name': user.lastName,
        Roles: user.roles.map((role) => role.name).join(', '),
      }))
      exportData(format, rows, 'Users', 'users')
    } finally {
      setIsExporting(false)
    }
  }

  const handleDelete = async (userId: string) => {
    const result = await confirmDeleteAlert({
      title: t('page.users.deleteTitle'),
      text: t('page.users.deleteConfirm'),
    })

    if (result.isConfirmed) {
      const response = await deleteUser({ id: userId })
      if (response.error) {
        apiErrorAlert(response.error)
        return
      }
      if (response.data?.success) {
        successToast.fire({
          text: t('page.users.deleteSuccess'),
        })
      } else if (response.data?.message) {
        await errorAlert({
          text: response.data?.message,
        })
      }
    }
  }

  const columnHelper = createColumnHelper<UserListDto>()
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const columns: ColumnDef<UserListDto, any>[] = [
    columnHelper.accessor('usernameNormalized', {
      header: t('table.columns.userName'),
      cell: (info) => info.getValue(),
    }),
    columnHelper.accessor('emailNormalized', {
      header: t('table.columns.email'),
      cell: (info) => info.getValue(),
    }),
    columnHelper.accessor('firstName', {
      header: t('table.columns.firstName'),
      cell: (info) => info.getValue(),
    }),
    columnHelper.accessor('lastName', {
      header: t('table.columns.lastName'),
      cell: (info) => info.getValue(),
    }),
    columnHelper.accessor('roles', {
      header: t('table.columns.roles'),
      cell: (info) =>
        info
          .getValue()
          .map((role: UserRoleDto) => role.name)
          .join(', '),
      enableSorting: false,
    }),
    columnHelper.accessor('isActive', {
      header: t('table.columns.isActive'),
      cell: (info) =>
        info.getValue() ? (
          <Badge variant="success">
            {t('table.filter.active')}
          </Badge>
        ) : (
          <Badge variant="danger">
            {t('table.filter.inactive')}
          </Badge>
        ),
    }),
    columnHelper.display({
      id: 'actions',
      header: t('table.actions'),
      cell: (info) => (
        <div className="flex items-center gap-2">
          {canUpdate && (
            <LocalizedLink href={`/admin/users/update/${info.row.original.id}`} className="btn btn-secondary btn-sm">
              <Pencil className="h-3 w-3" />
            </LocalizedLink>
          )}
          {canDelete && (
            <button
              type="button"
              className="btn cursor-pointer btn-danger btn-sm"
              onClick={() => handleDelete(info.row.original.id)}
              disabled={isDeletingUser}
            >
              {isDeletingUser ? <Loader2 className="animate-spin h-3 w-3" /> : <Trash2 className="h-3 w-3" />}
            </button>
          )}
        </div>
      ),
    }),
  ]

  if (getUsersApiError) {
    return (
      <div className="flex justify-center items-center">
        <ApiErrorMessages error={getUsersApiError} />
      </div>
    )
  }

  return (
    <DataTableProvider
      data={userListResponse?.items || []}
      rowCount={userListResponse?.total || 0}
      columns={columns}
      enableRowSelection={false}
      sorting={url.sorting}
      setSorting={url.setSorting}
      pagination={url.pagination}
      setPagination={url.setPagination}
      globalFilter={url.searchInput}
      setGlobalFilter={url.setGlobalFilter}
      isFetching={isGettingUsers}
    >
      <DataTableToolbar>
        {/* Filter Button in toolbar */}
        <UserFilterButton
          isOpen={filtersOpen}
          onToggle={() => setFiltersOpen(!filtersOpen)}
          activeFiltersCount={activeFiltersCount}
        />

        {canCreate && (
          <LocalizedLink href="/admin/users/create" className="btn flex items-center gap-2 btn-primary">
            <Plus size={16} />
            <span className="hidden sm:inline">{t('table.createLink')}</span>
          </LocalizedLink>
        )}
        <div className="dropdown">
          <Dropdown
            placement={`${isRTL ? 'bottom-start' : 'bottom-end'}`}
            btnClassName="btn btn-primary dropdown-toggle"
            isDisabled={isExporting || isGettingUsers || !userListResponse?.total}
            button={
              <div className="flex items-center gap-2">
                {isExporting ? <Loader2 className="animate-spin" size={16} /> : <Download size={16} />}
                <span className="hidden sm:inline">{t('table.export.button')}</span>
              </div>
            }
          >
            <ul className="mt-10">
              <li className="px-4 py-2 text-sm font-semibold text-gray-500 dark:text-gray-600">{t('table.export.excel')}</li>
              <li>
                <div role="menuitem" className="w-full cursor-pointer px-4 py-2 hover:bg-white-light dark:hover:bg-[#131E30]" onClick={() => handleExport('excel', false)}>
                  {t('table.export.currentPage')}
                </div>
              </li>
              <li>
                <div role="menuitem" className="w-full cursor-pointer px-4 py-2 hover:bg-white-light dark:hover:bg-[#131E30]" onClick={() => handleExport('excel', true)}>
                  {t('table.export.allRecords')}
                </div>
              </li>
              <li className="px-4 py-2 text-sm font-semibold text-gray-500 dark:text-gray-600">{t('table.export.csv')}</li>
              <li>
                <div role="menuitem" className="w-full cursor-pointer px-4 py-2 hover:bg-white-light dark:hover:bg-[#131E30]" onClick={() => handleExport('csv', false)}>
                  {t('table.export.currentPage')}
                </div>
              </li>
              <li>
                <div role="menuitem" className="w-full cursor-pointer px-4 py-2 hover:bg-white-light dark:hover:bg-[#131E30]" onClick={() => handleExport('csv', true)}>
                  {t('table.export.allRecords')}
                </div>
              </li>
            </ul>
          </Dropdown>
        </div>
      </DataTableToolbar>

      {/* Filter Panel - positioned between toolbar and table */}
      {filtersOpen && (
        <UserFilterPanel
          filters={pendingFilters}
          onChange={handleFilterChange}
          onSearch={handleSearch}
          onClear={handleClear}
        />
      )}
      <DataTable />

      <DataTablePagination siblingCount={1} />
    </DataTableProvider>
  )
}
