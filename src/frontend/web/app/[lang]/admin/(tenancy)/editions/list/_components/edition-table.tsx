'use client'
import { useState } from 'react'
import { useEditionListQuery, useLazyEditionListQuery, useEditionDeleteMutation, EditionListDto } from '@/store/api/tenancy'
import { SortDirection } from '@/store/api'
import { Trash2, Plus, Pencil, SlidersHorizontal } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { ExportFormat, successToast, exportData, isAllowed, apiErrorAlert, confirmDeleteAlert } from '@/lib/utils'
import { ApiErrorMessages, Badge } from '@/components/ui'
import { useAppSelector } from '@/store/hooks'
import { Allow } from '@/allow'
import { createColumnHelper, ColumnDef } from '@tanstack/react-table'
import { DataTableProvider, DataTableToolbar, DataTablePagination, DataTable, DataTableRowActions, DataTableToolbarButton, DataTableExportButton } from '@/components/ui/data-table'
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
          <DataTableRowActions
            actions={[
              {
                label: t('common.edit'),
                icon: <Pencil className="h-4 w-4" />,
                href: `/admin/editions/update/${edition.id}`,
                hidden: !canUpdate,
              },
              {
                label: t('page.features.editionTitle'),
                icon: <SlidersHorizontal className="h-4 w-4" />,
                href: `/admin/editions/features/${edition.id}`,
                hidden: !canViewFeatures,
              },
              {
                label: t('common.delete'),
                icon: <Trash2 className="h-4 w-4" />,
                variant: 'danger',
                onClick: () => handleDelete(edition.id),
                disabled: isDeletingEdition,
                hidden: !(canDelete && canBeDeleted),
              },
            ]}
          />
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
          <DataTableToolbarButton label={t('table.createLink')} icon={<Plus size={16} />} href="/admin/editions/create" />
        )}
        <DataTableExportButton onExport={handleExport} isExporting={isExporting} disabled={isGettingEditions || !editionListResponse?.total} />
      </DataTableToolbar>

      <DataTable />

      <DataTablePagination siblingCount={1} />
    </DataTableProvider>
  )
}
