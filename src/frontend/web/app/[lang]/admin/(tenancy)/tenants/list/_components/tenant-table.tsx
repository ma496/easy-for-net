'use client'
import { useEffect, useState } from 'react'
import {
  useTenantListQuery,
  useLazyTenantListQuery,
  useTenantDeleteMutation,
  useTenantSuspendMutation,
  useTenantReactivateMutation,
  TenantListDto,
  TenantStatus,
} from '@/store/api/tenancy'
import { SortDirection } from '@/store/api'
import { Download, Loader2, Trash2, Plus, Pencil, PauseCircle, PlayCircle, Users, Eye, LogIn } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { ExportFormat, successToast, exportData, isAllowed, apiErrorAlert, confirmDeleteAlert, confirmAlert, errorAlert } from '@/lib/utils'
import { Dropdown, LocalizedLink, ApiErrorMessages, Badge } from '@/components/ui'
import { useAppSelector } from '@/store/hooks'
import { Allow } from '@/allow'
import { createColumnHelper, ColumnDef } from '@tanstack/react-table'
import { DataTableProvider, DataTableToolbar, DataTablePagination, DataTable } from '@/components/ui/data-table'
import { TenantFilterPanel, TenantFilters } from './tenant-filter-panel'
import { TenantFilterButton } from './tenant-filter-button'
import { useTableUrlState, useTenantSwitch } from '@/hooks'
import { parseAsStringEnum } from 'nuqs'

/**
 * Interactive client-side data table that lists tenants with sorting, pagination, search and
 * lifecycle-status filtering synced to the URL, and permission-gated create, update, member,
 * suspend/reactivate and delete actions. The system-created tenant offers no lifecycle action,
 * since the API refuses to rename, suspend or delete it (AC-011).
 */
export const TenantTable = () => {
  const url = useTableUrlState({
    filters: {
      status: parseAsStringEnum(['Active', 'Suspended'] as const).withOptions({
        clearOnDefault: true,
        history: 'push',
      }),
    },
  })

  const [isExporting, setIsExporting] = useState(false)
  const [filtersOpen, setFiltersOpen] = useState(false)
  const [pendingFilters, setPendingFilters] = useState<TenantFilters>({
    status: url.filters.status ?? '',
  })
  const { t } = useTranslation()

  const isRTL = useAppSelector((state) => state.theme.rtlClass) === 'rtl'

  // When the filter panel opens, sync the draft values from the URL so the
  // user sees the currently-applied filters.
  useEffect(() => {
    if (filtersOpen) {
      setPendingFilters({ status: url.filters.status ?? '' })
    }
  }, [filtersOpen, url.filters.status])

  // Derived "applied" filters for the query and the active-count badge.
  const appliedFilters: TenantFilters = { status: url.filters.status ?? '' }
  const activeFiltersCount = [appliedFilters.status].filter(Boolean).length

  const {
    data: tenantListResponse,
    isFetching: isGettingTenants,
    error: getTenantsApiError,
  } = useTenantListQuery({
    page: url.page,
    pageSize: url.pageSize,
    sortField: url.sortField ?? undefined,
    sortDirection: url.sortDirection === 'desc' ? SortDirection.Desc : SortDirection.Asc,
    search: url.search || undefined,
    status: (appliedFilters.status || undefined) as TenantStatus | undefined,
  })

  const [fetchTenants] = useLazyTenantListQuery()
  const [deleteTenant, { isLoading: isDeletingTenant }] = useTenantDeleteMutation()
  const [suspendTenant, { isLoading: isSuspendingTenant }] = useTenantSuspendMutation()
  const [reactivateTenant, { isLoading: isReactivatingTenant }] = useTenantReactivateMutation()

  const authState = useAppSelector((state) => state.auth)
  const canCreate = isAllowed(authState, [Allow.Tenant_Create])
  const canUpdate = isAllowed(authState, [Allow.Tenant_Update])
  const canSuspend = isAllowed(authState, [Allow.Tenant_Suspend])
  const canReactivate = isAllowed(authState, [Allow.Tenant_Reactivate])
  const canDelete = isAllowed(authState, [Allow.Tenant_Delete])
  const canViewMembers = isAllowed(authState, [Allow.TenantMember_View])
  const canViewDetail = isAllowed(authState, [Allow.Tenant_Detail])
  // A platform account holds no membership anywhere, so this table is how it reaches a tenant at all:
  // entering one puts its session inside it, which is what lets it reproduce something a tenant has
  // reported rather than reason about it from outside. This is the account tier rather than a
  // permission - entering a tenant is not something a tenant grants - so it is read off the user
  // rather than through isAllowed.
  const canEnter = !!authState.user?.isPlatform
  const activeTenantId = authState.activeTenant?.id

  const { enterTenant, isBusy: isSwitchingTenant } = useTenantSwitch()

  const handleSearch = () => {
    url.filters.setMany({
      status: pendingFilters.status === '' ? null : (pendingFilters.status as 'Active' | 'Suspended'),
    })
    url.resetPage()
  }

  const handleClear = () => {
    setPendingFilters({ status: '' })
    url.filters.clearFilters()
    url.resetPage()
  }

  const handleFilterChange = (newFilters: TenantFilters) => {
    setPendingFilters(newFilters)
  }

  const handleExport = async (format: ExportFormat, all: boolean) => {
    setIsExporting(true)
    try {
      let dataToExport: TenantListDto[] = []
      if (all) {
        const response = await fetchTenants({
          page: url.page,
          pageSize: url.pageSize,
          sortField: url.sortField ?? undefined,
          sortDirection: url.sortDirection === 'desc' ? SortDirection.Desc : SortDirection.Asc,
          search: url.search || undefined,
          all: true,
          status: (appliedFilters.status || undefined) as TenantStatus | undefined,
        })
        if (response.data) {
          dataToExport = response.data?.items
        }
      } else {
        dataToExport = tenantListResponse?.items || []
      }

      if (dataToExport.length === 0) return

      // Every header and the sheet name are translated, because the export is read by the person who
      // asked for it and not by the API (AC-075).
      const rows = dataToExport.map((tenant) => ({
        [t('table.columns.name')]: tenant.name,
        [t('table.columns.identifier')]: tenant.identifier,
        [t('table.columns.status')]: tenant.status === TenantStatus.Active ? t('page.tenants.status.active') : t('page.tenants.status.suspended'),
      }))
      exportData(format, rows, t('page.tenants.title'), 'tenants')
    } finally {
      setIsExporting(false)
    }
  }

  const handleDelete = async (tenantId: string) => {
    const result = await confirmDeleteAlert({
      title: t('page.tenants.deleteTitle'),
      text: t('page.tenants.deleteConfirm'),
    })

    if (result.isConfirmed) {
      const response = await deleteTenant({ id: tenantId })
      if (response.error) {
        apiErrorAlert(response.error)
        return
      }
      if (response.data?.success) {
        successToast.fire({ text: t('page.tenants.deleteSuccess') })
      } else if (response.data?.message) {
        await errorAlert({ text: response.data?.message })
      }
    }
  }

  const handleSuspend = async (tenantId: string) => {
    const result = await confirmAlert({
      title: t('page.tenants.suspendTitle'),
      text: t('page.tenants.suspendConfirm'),
    })

    if (result.isConfirmed) {
      const response = await suspendTenant({ id: tenantId })
      if (response.error) {
        apiErrorAlert(response.error)
        return
      }
      successToast.fire({ text: t('page.tenants.suspendSuccess') })
    }
  }

  const handleReactivate = async (tenantId: string) => {
    const result = await confirmAlert({
      title: t('page.tenants.reactivateTitle'),
      text: t('page.tenants.reactivateConfirm'),
    })

    if (result.isConfirmed) {
      const response = await reactivateTenant({ id: tenantId })
      if (response.error) {
        apiErrorAlert(response.error)
        return
      }
      successToast.fire({ text: t('page.tenants.reactivateSuccess') })
    }
  }

  const isLifecycleBusy = isSuspendingTenant || isReactivatingTenant || isDeletingTenant

  const columnHelper = createColumnHelper<TenantListDto>()
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const columns: ColumnDef<TenantListDto, any>[] = [
    columnHelper.accessor('name', {
      header: t('table.columns.name'),
      cell: (info) => info.getValue(),
    }),
    columnHelper.accessor('identifier', {
      header: t('table.columns.identifier'),
      cell: (info) => info.getValue(),
    }),
    columnHelper.accessor('status', {
      header: t('table.columns.status'),
      cell: (info) =>
        info.getValue() === TenantStatus.Active ? (
          <Badge variant="success">{t('page.tenants.status.active')}</Badge>
        ) : (
          <Badge variant="danger">{t('page.tenants.status.suspended')}</Badge>
        ),
    }),
    columnHelper.accessor('systemCreated', {
      header: t('table.columns.type'),
      cell: (info) =>
        info.getValue() ? <Badge variant="info">{t('page.tenants.systemCreated')}</Badge> : null,
      enableSorting: false,
    }),
    columnHelper.display({
      id: 'actions',
      header: t('table.actions'),
      cell: (info) => {
        const tenant = info.row.original
        // The system-created tenant is protected from every lifecycle change (AC-011),
        // so those actions are omitted rather than shown and then refused.
        const canChangeLifecycle = !tenant.systemCreated

        return (
          <div className="flex items-center gap-2">
            {canUpdate && canChangeLifecycle && (
              <LocalizedLink href={`/admin/tenants/update/${tenant.id}`} className="btn btn-secondary btn-sm">
                <Pencil className="h-3 w-3" />
              </LocalizedLink>
            )}
            {canViewDetail && (
              <LocalizedLink href={`/admin/tenants/detail/${tenant.id}`} className="btn btn-secondary btn-sm" title={t('page.tenants.detail.title')}>
                <Eye className="h-3 w-3" />
              </LocalizedLink>
            )}
            {canViewMembers && (
              <LocalizedLink href={`/admin/tenants/members/${tenant.id}`} className="btn btn-secondary btn-sm" title={t('page.tenants.members.title')}>
                <Users className="h-3 w-3" />
              </LocalizedLink>
            )}
            {canEnter && tenant.status === TenantStatus.Active && (
              tenant.id === activeTenantId ? (
                <Badge variant="info">{t('page.tenants.enterCurrent')}</Badge>
              ) : (
                <button
                  type="button"
                  className="btn cursor-pointer btn-primary btn-sm"
                  onClick={() => enterTenant(tenant.id, tenant.name)}
                  disabled={isSwitchingTenant}
                  title={t('page.tenants.enterButton')}
                >
                  <LogIn className="h-3 w-3" />
                </button>
              )
            )}
            {canSuspend && canChangeLifecycle && tenant.status === TenantStatus.Active && (
              <button
                type="button"
                className="btn cursor-pointer btn-warning btn-sm"
                onClick={() => handleSuspend(tenant.id)}
                disabled={isLifecycleBusy}
                title={t('page.tenants.suspendTitle')}
              >
                {isSuspendingTenant ? <Loader2 className="animate-spin h-3 w-3" /> : <PauseCircle className="h-3 w-3" />}
              </button>
            )}
            {canReactivate && canChangeLifecycle && tenant.status === TenantStatus.Suspended && (
              <button
                type="button"
                className="btn cursor-pointer btn-success btn-sm"
                onClick={() => handleReactivate(tenant.id)}
                disabled={isLifecycleBusy}
                title={t('page.tenants.reactivateTitle')}
              >
                {isReactivatingTenant ? <Loader2 className="animate-spin h-3 w-3" /> : <PlayCircle className="h-3 w-3" />}
              </button>
            )}
            {canDelete && canChangeLifecycle && (
              <button
                type="button"
                className="btn cursor-pointer btn-danger btn-sm"
                onClick={() => handleDelete(tenant.id)}
                disabled={isLifecycleBusy}
                title={t('page.tenants.deleteTitle')}
              >
                {isDeletingTenant ? <Loader2 className="animate-spin h-3 w-3" /> : <Trash2 className="h-3 w-3" />}
              </button>
            )}
          </div>
        )
      },
    }),
  ]

  if (getTenantsApiError) {
    return (
      <div className="flex justify-center items-center">
        <ApiErrorMessages error={getTenantsApiError} />
      </div>
    )
  }

  return (
    <DataTableProvider
      data={tenantListResponse?.items || []}
      rowCount={tenantListResponse?.total || 0}
      columns={columns}
      enableRowSelection={false}
      sorting={url.sorting}
      setSorting={url.setSorting}
      pagination={url.pagination}
      setPagination={url.setPagination}
      globalFilter={url.searchInput}
      setGlobalFilter={url.setGlobalFilter}
      isFetching={isGettingTenants}
    >
      <DataTableToolbar>
        {/* Filter Button in toolbar */}
        <TenantFilterButton
          isOpen={filtersOpen}
          onToggle={() => setFiltersOpen(!filtersOpen)}
          activeFiltersCount={activeFiltersCount}
        />

        {canCreate && (
          <LocalizedLink href="/admin/tenants/create" className="btn flex items-center gap-2 btn-primary">
            <Plus size={16} />
            <span className="hidden sm:inline">{t('table.createLink')}</span>
          </LocalizedLink>
        )}
        <div className="dropdown">
          <Dropdown
            placement={`${isRTL ? 'bottom-start' : 'bottom-end'}`}
            btnClassName="btn btn-primary dropdown-toggle"
            isDisabled={isExporting || isGettingTenants || !tenantListResponse?.total}
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
        <TenantFilterPanel
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
