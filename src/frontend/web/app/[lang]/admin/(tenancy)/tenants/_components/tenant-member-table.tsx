'use client'
import { useState } from 'react'
import {
  useTenantMemberListQuery,
  useTenantMemberRemoveMutation,
  TenantMemberListDto,
  TenantMemberRoleDto,
} from '@/store/api/tenancy'
import { SortDirection } from '@/store/api'
import { Plus, Trash2, UserCog } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { successToast, isAllowed, apiErrorAlert, confirmDeleteAlert } from '@/lib/utils'
import { ApiErrorMessages, Badge } from '@/components/ui'
import { useAppSelector } from '@/store/hooks'
import { Allow } from '@/allow'
import { createColumnHelper, ColumnDef } from '@tanstack/react-table'
import { format } from 'date-fns'
import { DataTableProvider, DataTableToolbar, DataTablePagination, DataTable, DataTableRowActions, DataTableToolbarButton } from '@/components/ui/data-table'
import { useTableUrlState } from '@/hooks'
import { TenantMemberAddModal } from './tenant-member-add-modal'
import { TenantMemberRolesModal } from './tenant-member-roles-modal'

/**
 * Props for the TenantMemberTable, supplying the tenant whose members are listed.
 */
interface TenantMemberTableProps {
  tenantId: string
}

/**
 * Interactive client-side data table listing one tenant's members with sorting, pagination and search, and the
 * permission-gated actions that add a member, replace a member's roles and remove a member from this tenant alone
 * (AC-013): every action here is scoped to the tenant named in the route.
 */
export const TenantMemberTable = ({ tenantId }: TenantMemberTableProps) => {
  const url = useTableUrlState()
  const { t } = useTranslation()

  const [isAddOpen, setIsAddOpen] = useState(false)
  const [rolesMember, setRolesMember] = useState<TenantMemberListDto | null>(null)

  const {
    data: memberListResponse,
    isFetching: isGettingMembers,
    error: getMembersApiError,
  } = useTenantMemberListQuery({
    tenantId,
    page: url.page,
    pageSize: url.pageSize,
    sortField: url.sortField ?? undefined,
    sortDirection: url.sortDirection === 'desc' ? SortDirection.Desc : SortDirection.Asc,
    search: url.search || undefined,
  })

  const [removeMember, { isLoading: isRemovingMember }] = useTenantMemberRemoveMutation()

  const authState = useAppSelector((state) => state.auth)
  const canAdd = isAllowed(authState, [Allow.TenantMember_Add])
  const canUpdateRoles = isAllowed(authState, [Allow.TenantMember_UpdateRoles])
  const canRemove = isAllowed(authState, [Allow.TenantMember_Remove])

  const handleRemove = async (member: TenantMemberListDto) => {
    const result = await confirmDeleteAlert({
      title: t('page.tenants.members.removeTitle'),
      text: t('page.tenants.members.removeConfirm'),
    })

    if (result.isConfirmed) {
      const response = await removeMember({ tenantId, userId: member.id })
      if (response.error) {
        apiErrorAlert(response.error)
        return
      }
      successToast.fire({ text: t('page.tenants.members.removeSuccess') })
    }
  }

  const columnHelper = createColumnHelper<TenantMemberListDto>()
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const columns: ColumnDef<TenantMemberListDto, any>[] = [
    columnHelper.accessor('username', {
      header: t('table.columns.userName'),
      cell: (info) => info.getValue(),
    }),
    columnHelper.accessor('email', {
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
          .map((role: TenantMemberRoleDto) => role.name)
          .join(', '),
      enableSorting: false,
    }),
    columnHelper.accessor('memberSince', {
      header: t('table.columns.date'),
      cell: (info) => (
        <span className="text-gray-500 dark:text-gray-400">{format(new Date(info.getValue()), 'PP')}</span>
      ),
      enableSorting: false,
    }),
    columnHelper.accessor('isActive', {
      header: t('table.columns.isActive'),
      cell: (info) =>
        info.getValue() ? (
          <Badge variant="success">{t('table.filter.active')}</Badge>
        ) : (
          <Badge variant="danger">{t('table.filter.inactive')}</Badge>
        ),
    }),
    columnHelper.display({
      id: 'actions',
      header: t('table.actions'),
      cell: (info) => (
        <DataTableRowActions
          actions={[
            {
              label: t('page.tenants.members.rolesButton'),
              icon: <UserCog className="h-4 w-4" />,
              onClick: () => setRolesMember(info.row.original),
              hidden: !canUpdateRoles,
            },
            {
              label: t('page.tenants.members.removeTitle'),
              icon: <Trash2 className="h-4 w-4" />,
              variant: 'danger',
              onClick: () => handleRemove(info.row.original),
              disabled: isRemovingMember,
              hidden: !canRemove,
            },
          ]}
        />
      ),
    }),
  ]

  if (getMembersApiError) {
    return (
      <div className="flex justify-center items-center">
        <ApiErrorMessages error={getMembersApiError} />
      </div>
    )
  }

  return (
    <>
      <DataTableProvider
        data={memberListResponse?.items || []}
        rowCount={memberListResponse?.total || 0}
        columns={columns}
        enableRowSelection={false}
        sorting={url.sorting}
        setSorting={url.setSorting}
        pagination={url.pagination}
        setPagination={url.setPagination}
        globalFilter={url.searchInput}
        setGlobalFilter={url.setGlobalFilter}
        isFetching={isGettingMembers}
      >
        <DataTableToolbar>
          {canAdd && (
            <DataTableToolbarButton label={t('page.tenants.members.addButton')} icon={<Plus size={16} />} onClick={() => setIsAddOpen(true)} />
          )}
        </DataTableToolbar>

        <DataTable />

        <DataTablePagination siblingCount={1} />
      </DataTableProvider>

      <TenantMemberAddModal
        tenantId={tenantId}
        isOpen={isAddOpen}
        onClose={() => setIsAddOpen(false)}
      />
      <TenantMemberRolesModal
        tenantId={tenantId}
        member={rolesMember}
        isOpen={rolesMember !== null}
        onClose={() => setRolesMember(null)}
      />
    </>
  )
}
