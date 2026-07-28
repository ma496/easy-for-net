'use client'
import { useEffect, useState } from 'react'
import { useTranslation } from '@/i18n'
import { createColumnHelper, ColumnDef } from '@tanstack/react-table'
import { DataTableProvider } from '@/components/ui/data-table/context'
import { DataTableToolbar } from '@/components/ui/data-table/toolbar'
import { DataTablePagination } from '@/components/ui/data-table/pagination'
import { DataTable } from '@/components/ui/data-table'
import { ApiErrorMessages } from '@/components/ui/api-error-messages'
import { apiErrorAlert, confirmDeleteAlert, successToast } from '@/lib/utils'
import { NotificationDto } from '@/store/api/notifications/notifications-dtos'
import { NotificationType } from '@/store/api/notifications/enums'
import {
  useNotificationListQuery,
  useNotificationMarkAsReadMutation,
  useNotificationMarkAllAsReadMutation,
  useNotificationDeleteMutation
} from '@/store/api/notifications/notifications-api'
import { formatDistanceToNow } from 'date-fns'
import { Check, Trash2, CheckCheck, AlertCircle, AlertTriangle, CheckCircle, Info } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { LocalizedLink } from '@/components/localized-link'
import { Button } from '@/components/ui/button'
import { confirmAlert } from '@/lib/utils/notification'
import { NotificationFilterPanel, NotificationFilters } from './notification-filter-panel'
import { NotificationFilterButton } from './notification-filter-button'
import Truncated from '@/components/ui/truncated'
import { useTableUrlState } from '@/hooks/use-table-url-state'
import { parseAsString, parseAsStringEnum } from 'nuqs'
import { Loading } from '@/components/ui/loading'

/**
 * Interactive client-side data table that lists notifications with pagination, search, read/group filters synced to the URL, and per-row mark-as-read/delete plus a bulk mark-all-as-read action.
 */
export const NotificationTable = () => {
  const url = useTableUrlState({
    filters: {
      isRead: parseAsStringEnum(['true', 'false'] as const).withOptions({
        clearOnDefault: true,
        history: 'push',
      }),
      group: parseAsString.withOptions({
        clearOnDefault: true,
        history: 'push',
      }),
    },
  })

  const [filtersOpen, setFiltersOpen] = useState(false)
  const [pendingFilters, setPendingFilters] = useState<NotificationFilters>({
    isRead: url.filters.isRead ?? '',
    group: url.filters.group ?? '',
  })
  const { t } = useTranslation()

  const getIsReadValue = (value: string): boolean | null => {
    if (value === 'true') return true
    if (value === 'false') return false
    return null
  }

  // When the filter panel opens, sync the draft values from the URL.
  useEffect(() => {
    if (filtersOpen) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setPendingFilters({
        isRead: url.filters.isRead ?? '',
        group: url.filters.group ?? '',
      })
    }
  }, [filtersOpen, url.filters.isRead, url.filters.group])

  const appliedFilters: NotificationFilters = {
    isRead: url.filters.isRead ?? '',
    group: url.filters.group ?? '',
  }
  const activeFiltersCount = [appliedFilters.isRead, appliedFilters.group].filter(Boolean).length

  const {
    data: notificationResponse,
    isFetching: isGettingNotifications,
    error: getNotificationsError
  } = useNotificationListQuery({
    page: url.page,
    pageSize: url.pageSize,
    search: url.search || undefined,
    isRead: getIsReadValue(appliedFilters.isRead),
    group: appliedFilters.group || undefined
  })

  const [markAsRead, { isLoading: isMarkingAsRead }] = useNotificationMarkAsReadMutation()
  const [markAllAsRead, { isLoading: isMarkingAllAsRead }] = useNotificationMarkAllAsReadMutation()
  const [deleteNotification, { isLoading: isDeletingNotification }] = useNotificationDeleteMutation()

  const handleSearch = () => {
    url.filters.setMany({
      isRead: pendingFilters.isRead === '' ? null : (pendingFilters.isRead as 'true' | 'false'),
      group: pendingFilters.group === '' ? null : pendingFilters.group,
    })
    url.resetPage()
  }

  const handleClear = () => {
    const clearedFilters = { isRead: '', group: '' }
    setPendingFilters(clearedFilters)
    url.filters.clearFilters()
    url.resetPage()
  }

  const handleFilterChange = (newFilters: NotificationFilters) => {
    setPendingFilters(newFilters)
  }

  const handleMarkAsRead = async (id: string) => {
    const { error } = await markAsRead({ id })
    if (error) {
      apiErrorAlert(error)
      return
    } else {
      successToast.fire({
        text: t('notifications.markedAsRead')
      })
    }
  }

  const handleMarkAllAsRead = async () => {
    const result = await confirmAlert({
      title: t('notifications.markAllReadConfirmTitle'),
      text: t('notifications.markAllReadConfirmText')
    })
    if (result.isConfirmed) {
      const { error } = await markAllAsRead({})
      if (error) {
        apiErrorAlert(error)
        return
      } else {
        successToast.fire({
          text: t('notifications.markedAllAsRead')
        })
      }
    }
  }

  const handleDelete = async (id: string) => {
    const result = await confirmDeleteAlert({
      title: t('notifications.deleteTitle'),
      text: t('notifications.deleteConfirm')
    })
    if (result.isConfirmed) {
      const { error } = await deleteNotification({ id })
      if (error) {
        apiErrorAlert(error)
        return
      } else {
        successToast.fire({
          text: t('notifications.deleted')
        })
      }
    }
  }

  const getTypeIcon = (type: NotificationType) => {
    switch (type) {
      case NotificationType.Warning:
        return <AlertTriangle className="h-4 w-4 text-warning" />
      case NotificationType.Error:
        return <AlertCircle className="h-4 w-4 text-danger" />
      case NotificationType.Success:
        return <CheckCircle className="h-4 w-4 text-success" />
      default:
        return <Info className="h-4 w-4 text-primary" />
    }
  }

  const getTypeBadge = (type: NotificationType) => {
    switch (type) {
      case NotificationType.Warning:
        return <Badge variant="warning">{t('notifications.types.warning')}</Badge>
      case NotificationType.Error:
        return <Badge variant="danger">{t('notifications.types.error')}</Badge>
      case NotificationType.Success:
        return <Badge variant="success">{t('notifications.types.success')}</Badge>
      default:
        return <Badge variant="primary">{t('notifications.types.info')}</Badge>
    }
  }

  const columnHelper = createColumnHelper<NotificationDto>()
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const columns: ColumnDef<NotificationDto, any>[] = [
    columnHelper.accessor('type', {
      header: t('table.columns.type'),
      cell: (info) => (
        <div className="flex items-center gap-2">
          {getTypeIcon(info.getValue())}
          {getTypeBadge(info.getValue())}
        </div>
      ),
      enableSorting: false
    }),
    columnHelper.accessor('titleKey', {
      header: t('table.columns.title'),
      cell: (info) => (
        <LocalizedLink
          href={`/admin/notifications/${info.row.original.id}`}
          className="font-medium text-primary hover:underline"
        >
          {t(info.getValue())}
        </LocalizedLink>
      )
    }),
    columnHelper.accessor('messageKey', {
      header: t('table.columns.message'),
      cell: (info) => (
        <Truncated
          text={t(info.getValue())}
          className="text-gray-500 dark:text-gray-400"
          underline={false}
        />
      ),
      enableSorting: false
    }),
    columnHelper.accessor('group', {
      header: t('table.columns.group'),
      cell: (info) => {
        const group = info.getValue()
        return (
          <span className="text-gray-500 dark:text-gray-400">
            {group ? t(`notifications.groups.${group}`, { defaultValue: group }) : '-'}
          </span>
        )
      },
      enableSorting: false
    }),
    columnHelper.accessor('isRead', {
      header: t('table.columns.status'),
      cell: (info) => (
        info.getValue() ? (
          <Badge variant="secondary">{t('notifications.read')}</Badge>
        ) : (
          <Badge variant="primary">{t('notifications.unread')}</Badge>
        )
      )
    }),
    columnHelper.accessor('createdAt', {
      header: t('table.columns.date'),
      cell: (info) => (
        <span className="text-gray-500 dark:text-gray-400">
          {formatDistanceToNow(new Date(info.getValue()), { addSuffix: true })}
        </span>
      )
    }),
    columnHelper.display({
      id: 'actions',
      header: t('table.actions'),
      cell: (info) => (
        <div className="flex items-center gap-2">
          {!info.row.original.isRead && (
            <button
              type="button"
              className="btn cursor-pointer btn-secondary btn-sm"
              onClick={() => handleMarkAsRead(info.row.original.id)}
              title={t('notifications.markAsRead')}
              disabled={isMarkingAsRead || isMarkingAllAsRead || isDeletingNotification}
            >
              {isMarkingAsRead ? <Loading className="h-3 w-3" /> : <Check className="h-3 w-3" />}
            </button>
          )}
          <button
            type="button"
            className="btn cursor-pointer btn-danger btn-sm"
            onClick={() => handleDelete(info.row.original.id)}
            title={t('notifications.delete')}
            disabled={isMarkingAsRead || isMarkingAllAsRead || isDeletingNotification}
          >
            {isDeletingNotification ? <Loading className="h-3 w-3" /> : <Trash2 className="h-3 w-3" />}
          </button>
        </div>
      ),
    })
  ]

  if (getNotificationsError) {
    return (
      <div className="flex justify-center items-center">
        <ApiErrorMessages error={getNotificationsError} />
      </div>
    )
  }

  return (
    <DataTableProvider
      data={notificationResponse?.items || []}
      rowCount={notificationResponse?.total || 0}
      columns={columns}
      enableRowSelection={false}
      sorting={url.sorting}
      setSorting={url.setSorting}
      pagination={url.pagination}
      setPagination={url.setPagination}
      globalFilter={url.searchInput}
      setGlobalFilter={url.setGlobalFilter}
      isFetching={isGettingNotifications}
    >
      {filtersOpen && (
        <NotificationFilterPanel
          filters={pendingFilters}
          onChange={handleFilterChange}
          onSearch={handleSearch}
          onClear={handleClear}
        />
      )}

      <DataTableToolbar>
        <div className="flex items-center gap-2">
          <NotificationFilterButton
            isOpen={filtersOpen}
            onToggle={() => setFiltersOpen(!filtersOpen)}
            activeFiltersCount={activeFiltersCount}
          />
        </div>

        <Button
          variant="secondary"
          icon={<CheckCheck className="h-4 w-4" />}
          onClick={handleMarkAllAsRead}
          disabled={!notificationResponse?.items?.some(n => !n.isRead)}
        >
          {t('notifications.markAllRead')}
        </Button>
      </DataTableToolbar>

      <DataTable />
      <DataTablePagination siblingCount={1} />
    </DataTableProvider>
  )
}
