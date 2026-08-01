'use client'
import { useTranslation } from '@/i18n'
import { useNotificationListQuery } from '@/store/api/notifications'
import { NotificationItem } from './notification-item'
import { LocalizedLink } from '@/components/ui'
import Scrollbar from 'react-perfect-scrollbar'

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
    <div className="absolute inset-e-0 top-full z-50 mt-2 w-80 rounded-lg border border-gray-200 bg-white shadow-lg dark:border-gray-700 dark:bg-gray-800">
      <div className="flex items-center justify-between border-b border-gray-200 px-4 py-2 dark:border-gray-700">
        <h3 className="font-semibold">{t('common.notifications')}</h3>
        <button
          onClick={onClose}
          className="cursor-pointer text-gray-500 hover:text-gray-700 dark:hover:text-gray-300"
        >
          ×
        </button>
      </div>

      <Scrollbar
        className="max-h-96"
        options={{ suppressScrollX: true }}
      >
        {isNotificationsLoading ? (
          <div className="p-4 text-center text-gray-500">{t('common.loading')}</div>
        ) : notifications?.items.length === 0 ? (
          <div className="p-4 text-center text-gray-500">{t('notifications.noNotifications')}</div>
        ) : (
          notifications?.items.map((notification) => (
            <NotificationItem
              key={notification.id}
              notification={notification}
            />
          ))
        )}
      </Scrollbar>

      <div className="border-t border-gray-200 p-2 text-center dark:border-gray-700">
        <LocalizedLink
          href="/admin/notifications"
          className="text-sm text-primary hover:underline capitalize"
          onClick={onClose}
        >
          {t('common.viewAll')}
        </LocalizedLink>
      </div>
    </div>
  )
}

