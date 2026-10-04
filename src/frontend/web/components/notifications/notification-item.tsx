'use client'
import { useTranslation } from '@/i18n'
import { NotificationDto, NotificationType } from '@/store/api/notifications'
import { AlertTriangle, AlertCircle, CheckCircle, Info } from 'lucide-react'
import { LocalizedLink } from '@/components/ui'
import { cn, notificationVariables, formatRelativeTime } from '@/lib/utils'

/**
 * Props for the {@link NotificationItem} component, receiving the {@link NotificationDto} to display.
 */
interface NotificationItemProps {
  notification: NotificationDto
}

/** The icon and its tinted tile for each notification type. */
const typeStyles: Record<string, { icon: typeof Info; tile: string }> = {
  [NotificationType.Warning]: { icon: AlertTriangle, tile: 'bg-warning/12 text-warning' },
  [NotificationType.Error]: { icon: AlertCircle, tile: 'bg-danger/12 text-danger' },
  [NotificationType.Success]: { icon: CheckCircle, tile: 'bg-success/12 text-success' },
  [NotificationType.Info]: { icon: Info, tile: 'bg-info/12 text-info' },
}

/**
 * Renders a single notification row (type icon tile, translated title/message, relative timestamp) as a link to the notification details, with a dot and a tinted background while unread.
 */
export const NotificationItem = ({ notification }: NotificationItemProps) => {
  const { t, i18n } = useTranslation()
  const style = typeStyles[notification.type as string] ?? typeStyles[NotificationType.Info]

  return (
    <LocalizedLink
      href={`/admin/notifications/${notification.id}`}
      className={cn('flex items-start gap-3 rounded-lg px-3 py-2.5 transition-colors hover:bg-surface-2', !notification.isRead && 'bg-primary/5')}
    >
      <span className={cn('mt-0.5 flex size-8 shrink-0 items-center justify-center rounded-lg', style.tile)}>
        <style.icon size={16} />
      </span>
      <div className="min-w-0 flex-1">
        <div className="flex items-start justify-between gap-2">
          <h4 className="truncate text-sm font-medium text-foreground">{t(notification.titleKey, notificationVariables(notification.metadata))}</h4>
          {!notification.isRead && <span className="mt-1.5 size-2 shrink-0 rounded-full bg-primary" />}
        </div>
        <p className="mt-0.5 line-clamp-2 text-xs text-muted-foreground">{t(notification.messageKey, notificationVariables(notification.metadata))}</p>
        <span className="mt-1 block text-[11px] text-subtle-foreground">{formatRelativeTime(notification.createdAt, i18n.language)}</span>
      </div>
    </LocalizedLink>
  )
}
