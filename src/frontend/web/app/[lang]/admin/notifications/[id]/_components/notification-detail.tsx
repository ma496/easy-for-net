'use client'
import { useNotificationGetQuery, useNotificationMarkAsReadMutation, useNotificationMarkAsUnreadMutation, NotificationType } from '@/store/api/notifications'
import { useTranslation } from '@/i18n'
import { format } from 'date-fns'
import { AlertCircle, AlertTriangle, BellOff, CheckCircle, Info, Check, EyeOff } from 'lucide-react'
import { Badge, Button, ApiErrorMessages, Loader } from '@/components/ui'
import { apiErrorAlert, cn, notificationVariables, successToast, formatRelativeTime } from '@/lib/utils'

/**
 * Props for the NotificationDetail component, supplying the id of the notification to display.
 */
interface NotificationDetailProps {
  id: string
}

/**
 * Interactive client-side view that shows the full content of a single notification along with mark-as-read/mark-as-unread actions.
 */
export const NotificationDetail = ({ id }: NotificationDetailProps) => {
  const { t, i18n } = useTranslation()
  const { data: notification, isLoading: isNotificationLoading, error: notificationError } = useNotificationGetQuery({ id })
  const [markAsRead, { isLoading: isMarkingAsRead }] = useNotificationMarkAsReadMutation()
  const [markAsUnread, { isLoading: isMarkingAsUnread }] = useNotificationMarkAsUnreadMutation()

  const handleMarkAsRead = async () => {
    if (notification) {
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
  }

  const handleMarkAsUnread = async () => {
    if (notification) {
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
  }

  /** The icon and tint of the tile that heads the notification, by its type. */
  const getTypeTile = (type: NotificationType) => {
    switch (type) {
      case NotificationType.Warning:
        return { icon: <AlertTriangle className="h-5 w-5" />, className: 'bg-warning/10 text-warning' }
      case NotificationType.Error:
        return { icon: <AlertCircle className="h-5 w-5" />, className: 'bg-danger/10 text-danger' }
      case NotificationType.Success:
        return { icon: <CheckCircle className="h-5 w-5" />, className: 'bg-success/10 text-success' }
      default:
        return { icon: <Info className="h-5 w-5" />, className: 'bg-primary/10 text-primary' }
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

  if (isNotificationLoading) {
    return (
      <div className="flex min-h-64 items-center justify-center rounded-xl border border-border bg-surface shadow-xs">
        <Loader />
      </div>
    )
  }

  if (notificationError) {
    return (
      <div className="rounded-xl border border-border bg-surface p-5 shadow-xs">
        <ApiErrorMessages error={notificationError} />
      </div>
    )
  }

  if (!isNotificationLoading && !notificationError && !notification) {
    return (
      <div className="flex flex-col items-center justify-center gap-3 rounded-xl border border-border bg-surface px-6 py-14 text-center shadow-xs">
        <span className="grid size-12 place-content-center rounded-xl bg-surface-2 text-muted-foreground">
          <BellOff className="h-5 w-5" />
        </span>
        <p className="text-sm font-medium text-foreground">{t('notifications.notFound')}</p>
      </div>
    )
  }

  const tile = getTypeTile(notification.type)
  const variables = notificationVariables(notification.metadata)

  return (
    <div className="flex flex-col gap-6">
      <section className="rounded-xl border border-border bg-surface p-5 shadow-xs sm:p-6">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-start">
          <span className={cn('grid size-12 shrink-0 place-content-center rounded-xl', tile.className)}>{tile.icon}</span>
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2">
              <h2 className="text-lg font-semibold tracking-tight wrap-break-word text-foreground sm:text-xl">{t(notification.titleKey, variables)}</h2>
              {getTypeBadge(notification.type)}
              {notification.isRead ? (
                <Badge variant="secondary">{t('notifications.read')}</Badge>
              ) : (
                <Badge variant="primary" type="outline">
                  {t('notifications.unread')}
                </Badge>
              )}
            </div>
            <p className="mt-1 text-sm text-muted-foreground tabular-nums">{format(new Date(notification.createdAt), 'PPpp')}</p>
          </div>
          <div className="shrink-0">
            {notification.isRead ? (
              <Button variant="outline" size="sm" onClick={handleMarkAsUnread} isLoading={isMarkingAsUnread}>
                <EyeOff className="h-4 w-4" />
                {t('notifications.markAsUnread')}
              </Button>
            ) : (
              <Button variant="primary" size="sm" onClick={handleMarkAsRead} isLoading={isMarkingAsRead}>
                <Check className="h-4 w-4" />
                {t('notifications.markAsRead')}
              </Button>
            )}
          </div>
        </div>

        <p className="mt-5 border-t border-border pt-5 text-sm leading-relaxed wrap-break-word whitespace-pre-wrap text-foreground">{t(notification.messageKey, variables)}</p>

        <dl className="mt-5 grid gap-x-6 gap-y-4 border-t border-border pt-5 grid-cols-2 text-sm lg:grid-cols-4">
          <div className="min-w-0">
            <dt className="text-xs text-muted-foreground">{t('table.columns.group')}</dt>
            <dd className="mt-1 wrap-break-word text-foreground">
              {notification.group ? t(`notifications.groups.${notification.group}`, { defaultValue: notification.group }) : <span className="text-subtle-foreground">&mdash;</span>}
            </dd>
          </div>
          <div className="min-w-0">
            <dt className="text-xs text-muted-foreground">{t('table.columns.status')}</dt>
            <dd className="mt-1 text-foreground">{notification.isRead ? t('notifications.read') : t('notifications.unread')}</dd>
          </div>
          <div className="min-w-0">
            <dt className="text-xs text-muted-foreground">{t('table.columns.date')}</dt>
            <dd className="mt-1 text-foreground tabular-nums">{formatRelativeTime(notification.createdAt, i18n.language)}</dd>
          </div>
          {notification.createdAt !== notification.updatedAt && notification.updatedAt && (
            <div className="min-w-0">
              <dt className="text-xs text-muted-foreground">{t('table.columns.updated')}</dt>
              <dd className="mt-1 text-foreground">{formatRelativeTime(notification.updatedAt, i18n.language)}</dd>
            </div>
          )}
        </dl>
      </section>

      {notification.metadata && (
        <section className="min-w-0 rounded-xl border border-border bg-surface p-5 shadow-xs sm:p-6">
          <h3 className="mb-3 text-base font-semibold text-foreground">{t('notifications.metadata')}</h3>
          <pre className="max-h-96 overflow-auto rounded-lg bg-surface-2 p-4 font-mono text-xs leading-relaxed text-foreground" dir="ltr">
            {JSON.stringify(JSON.parse(notification.metadata), null, 2)}
          </pre>
        </section>
      )}
    </div>
  )
}
