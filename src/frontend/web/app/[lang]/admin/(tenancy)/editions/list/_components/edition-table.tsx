'use client'
import { useState } from 'react'
import { useEditionListQuery, useLazyEditionListQuery, useEditionDeleteMutation, EditionListDto } from '@/store/api/tenancy'
import { SortDirection } from '@/store/api'
import { Download, Loader2, Trash2, Plus, Pencil, SlidersHorizontal } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { ExportFormat, successToast, exportData, isAllowed, apiErrorAlert, confirmDeleteAlert } from '@/lib/utils'
import { Dropdown, LocalizedLink, ApiErrorMessages, Badge } from '@/components/ui'
import { useAppSelector } from '@/store/hooks'
import { Allow } from '@/allow'
import { createColumnHelper, ColumnDef } from '@tanstack/react-table'
import { DataTableProvider, DataTableToolbar, DataTablePagination, DataTable } from '@/components/ui/data-table'
import { useTableUrlState } from '@/hooks'

/**
 * Interactive client-side data table listing the plans the platform sells, with sorting, pagination
 * and search synced to the URL, and permission-gated create, update, entitlement and delete actions.
 * A plan any tenant is on offers no delete action: the API refuses it, because deleting it would
 * silently drop every tenant on it to the declared defaults.
 */
export const EditionTable = () => {
  const url = useTableUrlState()

  const [isExporting, setIsExporting] = useState(false)
  const { t } = useTranslation()

  const isRTL = useAppSelector((state) => state.theme.rtlClass) === 'rtl'

  const {
    data: editionListResponse,
    isFetching: isGettingEditions,
    error: getEditionsApiError,
  } = useEditionListQuery({
    page: url.page,
    pageSize: url.pageSize,
    sortField: url.sortField ?? undefined,
    sortDirection: url.sortDirection === 'desc' ? SortDirection.Desc : SortDirection.Asc,
    search: url.search || undefined,
  })

  const [fetchEditions] = useLazyEditionListQuery()
  const [deleteEdition, { isLoading: isDeletingEdition }] = useEditionDeleteMutation()

  const authState = useAppSelector((state) => state.auth)
  const canCreate = isAllowed(authState, [Allow.Edition_Create])
  const canUpdate = isAllowed(authState, [Allow.Edition_Update])
  const canDelete = isAllowed(authState, [Allow.Edition_Delete])
  const canViewFeatures = isAllowed(authState, [Allow.FeatureValue_View])

  const handleExport = async (format: ExportFormat, all: boolean) => {
    setIsExporting(true)
    try {
      let dataToExport: EditionListDto[] = []
      if (all) {
        const response = await fetchEditions({
          page: url.page,
          pageSize: url.pageSize,
          sortField: url.sortField ?? undefined,
          sortDirection: url.sortDirection === 'desc' ? SortDirection.Desc : SortDirection.Asc,
          search: url.search || undefined,
          all: true,
        })
        if (response.data) {
          dataToExport = response.data?.items
        }
      } else {
        dataToExport = editionListResponse?.items || []
      }

      if (dataToExport.length === 0) return

      const rows = dataToExport.map((edition) => ({
        [t('table.columns.name')]: edition.name,
        [t('table.columns.description')]: edition.description ?? '',
        [t('table.columns.displayOrder')]: edition.displayOrder,
        [t('table.columns.tenantCount')]: edition.tenantCount,
      }))
      exportData(format, rows, t('page.editions.title'), 'editions')
    } finally {
      setIsExporting(false)
    }
  }

  const handleDelete = async (editionId: string) => {
    const result = await confirmDeleteAlert({
      title: t('page.editions.deleteTitle'),
      text: t('page.editions.deleteConfirm'),
    })

    if (result.isConfirmed) {
      const response = await deleteEdition({ id: editionId })
      if (response.error) {
        apiErrorAlert(response.error)
        return
      }
      successToast.fire({ text: t('page.editions.deleteSuccess') })
    }
  }

  const columnHelper = createColumnHelper<EditionListDto>()
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const columns: ColumnDef<EditionListDto, any>[] = [
    columnHelper.accessor('name', {
      header: t('table.columns.name'),
      cell: (info) => info.getValue(),
    }),
    columnHelper.accessor('description', {
      header: t('table.columns.description'),
      cell: (info) => info.getValue() ?? '',
      enableSorting: false,
    }),
    columnHelper.accessor('displayOrder', {
      header: t('table.columns.displayOrder'),
      cell: (info) => info.getValue(),
    }),
    // Counted per page off the tenant rows rather than held on the plan, so it cannot be sorted on.
    columnHelper.accessor('tenantCount', {
      header: t('table.columns.tenantCount'),
      cell: (info) => (
        <div className="w-10 flex items-center justify-center">
          <Badge variant="primary">{info.getValue()}</Badge>
        </div>
      ),
      enableSorting: false,
    }),
    columnHelper.display({
      id: 'actions',
      header: t('table.actions'),
      cell: (info) => {
        const edition = info.row.original
        // A plan tenants are on cannot be deleted, so the action is omitted rather than shown and
        // then refused.
        const canBeDeleted = edition.tenantCount === 0

        return (
          <div className="flex items-center gap-2">
            {canUpdate && (
              <LocalizedLink href={`/admin/editions/update/${edition.id}`} className="btn btn-secondary btn-sm">
                <Pencil className="h-3 w-3" />
              </LocalizedLink>
            )}
            {canViewFeatures && (
              <LocalizedLink
                href={`/admin/editions/features/${edition.id}`}
                className="btn btn-secondary btn-sm"
                title={t('page.features.editionTitle')}
              >
                <SlidersHorizontal className="h-3 w-3" />
              </LocalizedLink>
            )}
            {canDelete && canBeDeleted && (
              <button
                type="button"
                className="btn cursor-pointer btn-danger btn-sm"
                onClick={() => handleDelete(edition.id)}
                disabled={isDeletingEdition}
                title={t('page.editions.deleteTitle')}
              >
                {isDeletingEdition ? <Loader2 className="animate-spin h-3 w-3" /> : <Trash2 className="h-3 w-3" />}
              </button>
            )}
          </div>
        )
      },
    }),
  ]

  if (getEditionsApiError) {
    return (
      <div className="flex justify-center items-center">
        <ApiErrorMessages error={getEditionsApiError} />
      </div>
    )
  }

  return (
    <DataTableProvider
      data={editionListResponse?.items || []}
      rowCount={editionListResponse?.total || 0}
      columns={columns}
      enableRowSelection={false}
      sorting={url.sorting}
      setSorting={url.setSorting}
      pagination={url.pagination}
      setPagination={url.setPagination}
      globalFilter={url.searchInput}
      setGlobalFilter={url.setGlobalFilter}
      isFetching={isGettingEditions}
    >
      <DataTableToolbar>
        {canCreate && (
          <LocalizedLink href="/admin/editions/create" className="btn flex items-center gap-2 btn-primary">
            <Plus size={16} />
            <span className="hidden sm:inline">{t('table.createLink')}</span>
          </LocalizedLink>
        )}
        <div className="dropdown">
          <Dropdown
            placement={`${isRTL ? 'bottom-start' : 'bottom-end'}`}
            btnClassName="btn btn-primary dropdown-toggle"
            isDisabled={isExporting || isGettingEditions || !editionListResponse?.total}
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

      <DataTable />

      <DataTablePagination siblingCount={1} />
    </DataTableProvider>
  )
}
