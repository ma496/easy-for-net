'use client'
import { useTranslation } from '@/i18n'
import { useNotificationListQuery } from '@/store/api/notifications'
import { NotificationItem } from './notification-item'
import { LocalizedLink } from '@/components/ui'
import Scrollbar from 'react-perfect-scrollbar'
import { BellOff, X } from 'lucide-react'

/**
 * Props for the {@link NotificationPanel} component, providing a callback to close the panel when navigating away or dismissing.
 */
interface NotificationPanelProps {
  onClose: () => void
}

/**
 * Dropdown panel that fetches the most recent notifications (first page, 5 items) and renders them in a scrollable list, with a header, empty/loading states, and a footer link to the full notifications page.
 */
export const NotificationPanel = ({ onClose }: NotificationPanelProps) => {
  const { t } = useTranslation()
  const { data: notifications, isLoading: isNotificationsLoading } = useNotificationListQuery({ page: 1, pageSize: 5 })

  return (
    <div className="fixed inset-x-2 top-14 z-50 w-auto max-w-[calc(100vw-1rem)] animate-scale-in overflow-hidden rounded-xl border border-border bg-surface shadow-lg sm:absolute sm:inset-x-auto sm:inset-e-0 sm:top-full sm:mt-1.5 sm:w-96">
      <div className="flex items-center justify-between border-b border-border px-4 py-3">
        <h3 className="text-sm font-semibold">{t('common.notifications')}</h3>
        <button type="button" onClick={onClose} className="icon-btn -me-2 size-7" aria-label={t('common.close')}>
          <X size={15} />
        </button>
      </div>

      <Scrollbar className="max-h-96 p-1.5" options={{ suppressScrollX: true }}>
        {isNotificationsLoading ? (
          <div className="px-4 py-8 text-center text-sm text-muted-foreground">{t('common.loading')}</div>
        ) : notifications?.items.length === 0 ? (
          <div className="flex flex-col items-center gap-2 px-4 py-10 text-center text-sm text-muted-foreground">
            <span className="flex size-10 items-center justify-center rounded-xl bg-surface-2">
              <BellOff size={18} className="text-subtle-foreground" />
            </span>
            {t('notifications.noNotifications')}
          </div>
        ) : (
          notifications?.items.map((notification) => <NotificationItem key={notification.id} notification={notification} />)
        )}
      </Scrollbar>

      <div className="border-t border-border bg-surface-2/50 p-1.5 text-center">
        <LocalizedLink href="/admin/notifications" className="block rounded-md py-1.5 text-[13px] font-medium text-primary transition-colors hover:bg-surface-2" onClick={onClose}>
          {t('common.viewAll')}
        </LocalizedLink>
      </div>
    </div>
  )
}
