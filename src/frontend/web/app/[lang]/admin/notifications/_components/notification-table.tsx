'use client'
import { useEffect, useState } from 'react'
import { useTranslation } from '@/i18n'
import { createColumnHelper, ColumnDef } from '@tanstack/react-table'
import { DataTableProvider, DataTableToolbar, DataTablePagination, DataTable, DataTableRowActions, DataTableFilterButton, DataTableToolbarButton } from '@/components/ui/data-table'
import { ApiErrorMessages, Badge, LocalizedLink, Truncated } from '@/components/ui'
import { apiErrorAlert, cn, confirmAlert, confirmDeleteAlert, notificationVariables, successToast, formatRelativeTime } from '@/lib/utils'
import {
  NotificationDto,
  NotificationType,
  useNotificationListQuery,
  useNotificationMarkAsReadMutation,
  useNotificationMarkAsUnreadMutation,
  useNotificationMarkAllAsReadMutation,
  useNotificationDeleteMutation,
} from '@/store/api/notifications'
import { Check, Trash2, Mail, CheckCheck, AlertCircle, AlertTriangle, CheckCircle, Info } from 'lucide-react'
import { NotificationFilterPanel, NotificationFilters } from './notification-filter-panel'
import { useTableUrlState } from '@/hooks'
import { parseAsString, parseAsStringEnum } from 'nuqs'

/**
 * Interactive client-side data table that lists notifications with pagination, search, read/group filters synced to the URL, and per-row mark-as-read/mark-as-unread/delete plus a bulk mark-all-as-read action.
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
  const { t, i18n } = useTranslation()

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
    error: getNotificationsError,
  } = useNotificationListQuery({
    page: url.page,
    pageSize: url.pageSize,
    search: url.search || undefined,
    isRead: getIsReadValue(appliedFilters.isRead),
    group: appliedFilters.group || undefined,
  })

  const [markAsRead, { isLoading: isMarkingAsRead }] = useNotificationMarkAsReadMutation()
  const [markAsUnread, { isLoading: isMarkingAsUnread }] = useNotificationMarkAsUnreadMutation()
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
        text: t('notifications.markedAsRead'),
      })
    }
  }

  const handleMarkAsUnread = async (id: string) => {
    const { error } = await markAsUnread({ id })
    if (error) {
      apiErrorAlert(error)
      return
    } else {
      successToast.fire({
        text: t('notifications.markedAsUnread'),
      })
    }
  }

  const handleMarkAllAsRead = async () => {
    const result = await confirmAlert({
      title: t('notifications.markAllReadConfirmTitle'),
      text: t('notifications.markAllReadConfirmText'),
    })
    if (result.isConfirmed) {
      const { error } = await markAllAsRead({})
      if (error) {
        apiErrorAlert(error)
        return
      } else {
        successToast.fire({
          text: t('notifications.markedAllAsRead'),
        })
      }
    }
  }

  const handleDelete = async (id: string) => {
    const result = await confirmDeleteAlert({
      title: t('notifications.deleteTitle'),
      text: t('notifications.deleteConfirm'),
    })
    if (result.isConfirmed) {
      const { error } = await deleteNotification({ id })
      if (error) {
        apiErrorAlert(error)
        return
      } else {
        successToast.fire({
          text: t('notifications.deleted'),
        })
      }
    }
  }

  /** The type's badge, carrying its icon so the column reads at a glance. */
  const getTypeBadge = (type: NotificationType) => {
    switch (type) {
      case NotificationType.Warning:
        return (
          <Badge variant="warning">
            <AlertTriangle className="h-3 w-3" />
            {t('notifications.types.warning')}
          </Badge>
        )
      case NotificationType.Error:
        return (
          <Badge variant="danger">
            <AlertCircle className="h-3 w-3" />
            {t('notifications.types.error')}
          </Badge>
        )
      case NotificationType.Success:
        return (
          <Badge variant="success">
            <CheckCircle className="h-3 w-3" />
            {t('notifications.types.success')}
          </Badge>
        )
      default:
        return (
          <Badge variant="primary">
            <Info className="h-3 w-3" />
            {t('notifications.types.info')}
          </Badge>
        )
    }
  }

  const isRowActionPending = isMarkingAsRead || isMarkingAsUnread || isMarkingAllAsRead || isDeletingNotification

  const columnHelper = createColumnHelper<NotificationDto>()
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const columns: ColumnDef<NotificationDto, any>[] = [
    columnHelper.accessor('type', {
      header: t('table.columns.type'),
      cell: (info) => getTypeBadge(info.getValue()),
      enableSorting: false,
    }),
    columnHelper.accessor('titleKey', {
      meta: { card: 'title' },
      header: t('table.columns.title'),
      cell: (info) => (
        <span className="inline-flex min-w-0 items-center gap-2">
          {/* An unread notification is marked with a dot and a heavier title. */}
          {!info.row.original.isRead && <span className="size-2 shrink-0 rounded-full bg-primary" aria-hidden="true" />}
          <LocalizedLink
            href={`/admin/notifications/${info.row.original.id}`}
            className={cn('text-foreground hover:text-primary hover:underline', info.row.original.isRead ? 'font-normal' : 'font-semibold')}
          >
            {t(info.getValue(), notificationVariables(info.row.original.metadata))}
          </LocalizedLink>
        </span>
      ),
    }),
    columnHelper.accessor('messageKey', {
      meta: { card: 'subtitle' },
      header: t('table.columns.message'),
      cell: (info) => <Truncated text={t(info.getValue(), notificationVariables(info.row.original.metadata))} className="text-muted-foreground" underline={false} />,
      enableSorting: false,
    }),
    columnHelper.accessor('group', {
      header: t('table.columns.group'),
      cell: (info) => {
        const group = info.getValue()
        return <span className="text-muted-foreground">{group ? t(`notifications.groups.${group}`, { defaultValue: group }) : <span className="text-subtle-foreground">&mdash;</span>}</span>
      },
      enableSorting: false,
    }),
    columnHelper.accessor('isRead', {
      meta: { card: 'badge' },
      header: t('table.columns.status'),
      cell: (info) =>
        info.getValue() ? (
          <Badge variant="secondary">{t('notifications.read')}</Badge>
        ) : (
          <Badge variant="primary" type="outline">
            {t('notifications.unread')}
          </Badge>
        ),
    }),
    columnHelper.accessor('createdAt', {
      header: t('table.columns.date'),
      cell: (info) => <span className="whitespace-nowrap text-muted-foreground">{formatRelativeTime(info.getValue(), i18n.language)}</span>,
    }),
    columnHelper.display({
      id: 'actions',
      header: t('table.actions'),
      cell: (info) => (
        <DataTableRowActions
          actions={[
            {
              label: t('notifications.markAsRead'),
              icon: <Check className="h-4 w-4" />,
              onClick: () => handleMarkAsRead(info.row.original.id),
              disabled: isRowActionPending,
              hidden: info.row.original.isRead,
            },
            {
              label: t('notifications.markAsUnread'),
              icon: <Mail className="h-4 w-4" />,
              onClick: () => handleMarkAsUnread(info.row.original.id),
              disabled: isRowActionPending,
              hidden: !info.row.original.isRead,
            },
            {
              label: t('notifications.delete'),
              icon: <Trash2 className="h-4 w-4" />,
              variant: 'danger',
              onClick: () => handleDelete(info.row.original.id),
              disabled: isRowActionPending,
            },
          ]}
        />
      ),
    }),
  ]

  if (getNotificationsError) {
    return (
      <div className="flex items-center justify-center">
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
      <DataTableToolbar>
        <DataTableFilterButton isOpen={filtersOpen} onToggle={() => setFiltersOpen(!filtersOpen)} activeFiltersCount={activeFiltersCount} />

        <DataTableToolbarButton label={t('notifications.markAllRead')} icon={<CheckCheck size={16} />} onClick={handleMarkAllAsRead} disabled={!notificationResponse?.items?.some((n) => !n.isRead)} />
      </DataTableToolbar>

      {filtersOpen && <NotificationFilterPanel filters={pendingFilters} onChange={handleFilterChange} onSearch={handleSearch} onClear={handleClear} />}
      <DataTable cardsBelow="lg" />
      <DataTablePagination siblingCount={1} />
    </DataTableProvider>
  )
}
