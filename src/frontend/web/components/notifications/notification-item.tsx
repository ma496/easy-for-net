'use client'
import { useTranslation } from '@/i18n'
import { NotificationDto } from '@/store/api/notifications/notifications-dtos'
import { NotificationType } from '@/store/api/notifications/enums'
import { formatDistanceToNow } from 'date-fns'
import { AlertTriangle, AlertCircle, CheckCircle, Info } from 'lucide-react'
import { LocalizedLink } from '@/components/ui'

/**
 * Props for the {@link NotificationItem} component, receiving the {@link NotificationDto} to display.
 */
interface NotificationItemProps {
  notification: NotificationDto
}

/**
 * Renders a single notification row (icon, translated title/message, relative timestamp) as a link to the notification details, color-coded by type and styled differently when unread.
 */
export const NotificationItem = ({ notification }: NotificationItemProps) => {
  const { t } = useTranslation()

  const getTypeStyles = (type: NotificationType | string) => {
    const colors: Record<string, { border: string, bg: string }> = {
      [NotificationType.Warning]: { border: '#e2a03f', bg: 'rgba(226,160,63,0.1)' },
      [NotificationType.Error]: { border: '#e7515a', bg: 'rgba(231,81,90,0.1)' },
      [NotificationType.Success]: { border: '#00ab55', bg: 'rgba(0,171,85,0.1)' },
      [NotificationType.Info]: { border: '#4361ee', bg: 'rgba(67,97,238,0.1)' }
    }
    return colors[type as string] || colors[NotificationType.Info]
  }

  const getTypeIcon = (type: NotificationType | string) => {
    switch (type) {
      case NotificationType.Warning:
        return <AlertTriangle className="h-4 w-4 text-yellow-500" />
      case NotificationType.Error:
        return <AlertCircle className="h-4 w-4 text-red-500" />
      case NotificationType.Success:
        return <CheckCircle className="h-4 w-4 text-green-500" />
      default:
        return <Info className="h-4 w-4 text-blue-500" />
    }
  }

  const typeStyles = getTypeStyles(notification.type)

  return (
    <LocalizedLink
      href={`/admin/notifications/${notification.id}`}
      className={`block p-3 border-b border-gray-100 dark:border-gray-700 ltr:border-s-4 rtl:border-e-4 ${!notification.isRead ? 'bg-gray-50 dark:bg-gray-700/50' : ''}`}
      style={{ borderColor: typeStyles.border }}
    >
      <div className="flex items-start justify-between">
        <div className="flex items-start gap-2">
          <div className="mt-0.5">{getTypeIcon(notification.type)}</div>
          <div>
            <h4 className="text-sm font-medium">{t(notification.titleKey)}</h4>
            <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">{t(notification.messageKey)}</p>
            <span className="mt-1 block text-xs text-gray-400 dark:text-gray-500">
              {formatDistanceToNow(new Date(notification.createdAt), { addSuffix: true })}
            </span>
          </div>
        </div>
        {!notification.isRead && (
          <span className="h-2 w-2 shrink-0 rounded-full bg-primary" />
        )}
      </div>
    </LocalizedLink>
  )
}

