'use client'
import { useNotificationGetQuery, useNotificationMarkAsReadMutation, useNotificationMarkAsUnreadMutation } from '@/store/api/notifications/notifications/notifications-api'
import { useTranslation } from '@/i18n'
import { formatDistanceToNow, format } from 'date-fns'
import { AlertCircle, AlertTriangle, CheckCircle, Info, Check, EyeOff } from 'lucide-react'
import { Badge, Button, ApiErrorMessages, Loading } from '@/components/ui'
import { NotificationType } from '@/store/api/notifications/enums'
import { successToast } from '@/lib/utils/notification'
import { apiErrorAlert } from '@/lib/utils'

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
  const { t } = useTranslation()
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
          text: t('notifications.markedAsRead')
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
          text: t('notifications.markedAsUnread')
        })
      }
    }
  }

  const getTypeIcon = (type: NotificationType) => {
    switch (type) {
      case NotificationType.Warning:
        return <AlertTriangle className="h-5 w-5 text-warning" />
      case NotificationType.Error:
        return <AlertCircle className="h-5 w-5 text-danger" />
      case NotificationType.Success:
        return <CheckCircle className="h-5 w-5 text-success" />
      default:
        return <Info className="h-5 w-5 text-primary" />
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
      <div className="flex items-center justify-center">
        <Loading />
      </div>
    )
  }

  if (notificationError) {
    return (
      <div className="flex items-center justify-center">
        <ApiErrorMessages error={notificationError} />
      </div>
    )
  }

  if (!isNotificationLoading && !notificationError && !notification) {
    return (
      <div className="flex items-center justify-center">
        {t('notifications.notFound')}
      </div>
    )
  }

  return (
    <div>
      <div className="flex items-center justify-end gap-2 my-2">
        {notification.isRead ? (
          <Button
            variant="secondary"
            size="sm"
            onClick={handleMarkAsUnread}
            isLoading={isMarkingAsUnread}
          >
            <EyeOff className="h-4 w-4" />
            {t('notifications.markAsUnread')}
          </Button>
        ) : (
          <Button
            variant="success"
            size="sm"
            onClick={handleMarkAsRead}
            isLoading={isMarkingAsRead}
          >
            <Check className="h-4 w-4" />
            {t('notifications.markAsRead')}
          </Button>
        )}
      </div>

      <div className="rounded-lg border border-gray-200 bg-white p-6 dark:border-gray-700 dark:bg-gray-800">
        <div className="flex items-start gap-4">
          <div className="mt-1">{getTypeIcon(notification.type)}</div>
          <div className="flex-1 space-y-4">
            <div className="flex items-center gap-2">
              <h2 className="text-xl font-semibold">{t(notification.titleKey)}</h2>
              {getTypeBadge(notification.type)}
              {notification.isRead && (
                <Badge variant="secondary">{t('notifications.read')}</Badge>
              )}
            </div>

            <p className="text-gray-600 dark:text-gray-300">
              {t(notification.messageKey)}
            </p>

            {notification.metadata && (
              <div className="rounded bg-gray-50 p-4 dark:bg-gray-700">
                <h4 className="mb-2 text-sm font-medium text-gray-500 dark:text-gray-400">
                  {t('notifications.metadata')}
                </h4>
                <pre className="text-sm text-gray-700 dark:text-gray-300">
                  {JSON.stringify(JSON.parse(notification.metadata), null, 2)}
                </pre>
              </div>
            )}

            <div className="flex flex-wrap gap-4 text-sm text-gray-500 dark:text-gray-400">
              <div>
                <span className="font-medium">{t('table.columns.date')}:</span>{' '}
                {format(new Date(notification.createdAt), 'PPpp')}
              </div>
              {notification.createdAt !== notification.updatedAt && notification.updatedAt && (
                <div>
                  <span className="font-medium">{t('table.columns.updated')}:</span>{' '}
                  {formatDistanceToNow(new Date(notification.updatedAt), { addSuffix: true })}
                </div>
              )}
            </div>

            {notification.group && (
              <div className="text-sm text-gray-500 dark:text-gray-400">
                <span className="font-medium">{t('table.columns.group')}:</span>{' '}
                {notification.group}
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
