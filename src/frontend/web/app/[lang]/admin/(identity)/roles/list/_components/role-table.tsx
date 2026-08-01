'use client'
import { createColumnHelper, ColumnDef } from '@tanstack/react-table'
import { DataTableProvider } from '@/components/ui/data-table/context'
import { DataTableToolbar } from '@/components/ui/data-table/toolbar'
import { DataTablePagination } from '@/components/ui/data-table/pagination'
import { DataTable } from '@/components/ui/data-table'
import { useState } from 'react'
import { useRoleListQuery, useLazyRoleListQuery, useRoleDeleteMutation } from '@/store/api/identity/roles/roles-api'
import { SortDirection } from '@/store/api/base/sort-direction'
import { RoleListDto } from '@/store/api/identity/roles/roles-dtos'
import { Download, Loader2, Trash2, Plus, Pencil, Shield } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { apiErrorAlert, exportData, ExportFormat, isAllowed } from '@/lib/utils'
import { Dropdown } from '@/components/ui/dropdown'
import { Badge } from '@/components/ui/badge'
import { useAppSelector } from '@/store/hooks'
import { LocalizedLink } from '@/components/ui/localized-link'
import { ApiErrorMessages } from '@/components/ui/api-error-messages'
import { Allow } from '@/allow'
import { confirmDeleteAlert, errorAlert, successToast } from '@/lib/utils'
import { useTableUrlState } from '@/hooks/use-table-url-state'

/**
 * Interactive client-side data table that lists roles with sorting, pagination, search, and per-row update/delete/change-permissions actions gated by the user's permissions.
 * Also supports exporting the current page or the full filtered set to Excel or CSV.
 */
export const RoleTable = () => {
  const url = useTableUrlState()

  const [isExporting, setIsExporting] = useState(false)
  const { t } = useTranslation()
  const isRTL = useAppSelector((state) => state.theme.rtlClass) === 'rtl'

  const {
    data: roleListResponse,
    isFetching: isGettingRoles,
    error: roleListError
  } = useRoleListQuery({
    page: url.page,
    pageSize: url.pageSize,
    sortField: url.sortField ?? undefined,
    sortDirection: url.sortDirection === 'desc' ? SortDirection.Desc : SortDirection.Asc,
    search: url.search || undefined,
  })

  const [fetchRoles] = useLazyRoleListQuery()
  const [deleteRole, { isLoading: isDeletingRole }] = useRoleDeleteMutation()

  const authState = useAppSelector((state) => state.auth)
  const canCreate = isAllowed(authState, [Allow.Role_Create])
  const canUpdate = isAllowed(authState, [Allow.Role_Update])
  const canDelete = isAllowed(authState, [Allow.Role_Delete])
  const canChangePermissions = isAllowed(authState, [Allow.Role_ChangePermissions])

  const handleExport = async (format: ExportFormat, all: boolean) => {
    setIsExporting(true)
    try {
      let dataToExport: RoleListDto[] = []
      if (all) {
        const response = await fetchRoles({
          page: url.page,
          pageSize: url.pageSize,
          sortField: url.sortField ?? undefined,
          sortDirection: url.sortDirection === 'desc' ? SortDirection.Desc : SortDirection.Asc,
          search: url.search || undefined,
          all: true,
        })
        if (response.data) {
          dataToExport = response.data.items
        }
      } else {
        dataToExport = roleListResponse?.items || []
      }

      if (dataToExport.length === 0) return

      const rows = dataToExport.map((role) => ({
        Name: role.name,
        Description: role.description,
        Users: role.userCount,
      }))
      exportData(format, rows, 'Roles', 'roles')
    } finally {
      setIsExporting(false)
    }
  }

  const handleDelete = async (roleId: string) => {
    const result = await confirmDeleteAlert({
      title: t('page.roles.deleteTitle'),
      text: t('page.roles.deleteConfirm'),
    })

    if (result.isConfirmed) {
      const response = await deleteRole({ id: roleId })
      if (response.error) {
        apiErrorAlert(response.error)
        return
      }
      if (response.data?.success) {
        successToast.fire({
          text: t('page.roles.deleteSuccess'),
        })
      } else if (response.data?.message) {
        await errorAlert({
          text: response.data.message,
        })
      }
    }
  }

  const columnHelper = createColumnHelper<RoleListDto>()
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const columns: ColumnDef<RoleListDto, any>[] = [
    columnHelper.accessor('name', {
      header: t('table.columns.name'),
      cell: (info) => info.getValue(),
    }),
    columnHelper.accessor('description', {
      header: t('table.columns.description'),
      cell: (info) => info.getValue(),
    }),
    columnHelper.accessor('userCount', {
      header: t('table.columns.userCount'),
      cell: (info) => (
        <div className='w-10 flex items-center justify-center'>
          <Badge variant='primary'>{info.getValue()}</Badge>
        </div>),
      enableSorting: false,
    }),
    columnHelper.display({
      id: 'actions',
      header: t('table.actions'),
      cell: (info) => (
        <div className="flex items-center gap-2">
          {canUpdate && (
            <LocalizedLink href={`/admin/roles/update/${info.row.original.id}`} className="btn btn-secondary btn-sm">
              <Pencil className="h-3 w-3" />
            </LocalizedLink>
          )}
          {canDelete && (
            <button
              type="button"
              className="btn cursor-pointer btn-danger btn-sm"
              disabled={isDeletingRole}
              onClick={() => handleDelete(info.row.original.id)}>
              {isDeletingRole ? <Loader2 className="animate-spin h-3 w-3" /> : <Trash2 className="h-3 w-3" />}
            </button>
          )}
          {canChangePermissions && (
            <LocalizedLink href={`/admin/roles/change-permissions/${info.row.original.id}`} className="btn btn-primary btn-sm">
              <Shield className="h-3 w-3" />
            </LocalizedLink>
          )}
        </div>
      ),
    }),
  ]

  if (roleListError) {
    return (
      <div className="flex justify-center items-center">
        <ApiErrorMessages error={roleListError} />
      </div>
    )
  }

  return (
    <DataTableProvider
      data={roleListResponse?.items || []}
      rowCount={roleListResponse?.total || 0}
      columns={columns}
      enableRowSelection={false}
      sorting={url.sorting}
      setSorting={url.setSorting}
      pagination={url.pagination}
      setPagination={url.setPagination}
      globalFilter={url.searchInput}
      setGlobalFilter={url.setGlobalFilter}
      isFetching={isGettingRoles}
    >
      <DataTableToolbar>
        {canCreate && (
          <LocalizedLink href="/admin/roles/create" className="btn flex items-center gap-2 btn-primary">
            <Plus size={16} />
            <span>{t('table.createLink')}</span>
          </LocalizedLink>
        )}
        <div className="dropdown">
          <Dropdown
            placement={`${isRTL ? 'bottom-start' : 'bottom-end'}`}
            btnClassName="btn btn-primary dropdown-toggle"
            isDisabled={isExporting || isGettingRoles || !roleListResponse?.total}
            button={
              <div className="flex items-center gap-2">
                {isExporting ? <Loader2 className="animate-spin" size={16} /> : <Download size={16} />}
                <span className="">{t('table.export.button')}</span>
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
      <DataTable />
      <DataTablePagination siblingCount={1} />
    </DataTableProvider>
  )
}
