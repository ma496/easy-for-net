'use client'
import { useState, useRef, useEffect } from 'react'
import { Bell } from 'lucide-react'
import { useAppSelector } from '@/store/hooks'
import { useTranslation } from '@/i18n'
import { NotificationPanel } from './notification-panel'
import { formatUnreadBadge } from './format-unread-badge'

/**
 * Header bell button that toggles the {@link NotificationPanel} and shows the unread count from the notifications slice as a badge (`formatUnreadBadge`: none at zero, capped at "99+").
 */
export const NotificationBell = () => {
  const { t } = useTranslation()
  const [isOpen, setIsOpen] = useState(false)
  const unreadCount = useAppSelector(state => state.notifications.unreadCount)
  const badge = formatUnreadBadge(unreadCount)
  const panelRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (panelRef.current && !panelRef.current.contains(event.target as Node)) {
        setIsOpen(false)
      }
    }
    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside)
    }
    return () => {
      document.removeEventListener('mousedown', handleClickOutside)
    }
  }, [isOpen])

  return (
    <div className="relative" ref={panelRef}>
      <button
        type="button"
        className="relative flex h-9 w-9 cursor-pointer items-center justify-center rounded-full bg-white-light/40 p-2 hover:bg-white-light/90 hover:text-primary dark:bg-dark/40 dark:hover:bg-dark/60 dark:hover:text-primary"
        aria-expanded={isOpen}
        onClick={() => setIsOpen(!isOpen)}
      >
        <span className="sr-only">{t('common.notifications')}</span>
        <Bell className="h-5 w-5" aria-hidden="true" />
        {badge !== null && (
          <span className="absolute -top-1 -end-1 flex h-5 min-w-5 items-center justify-center rounded-full bg-danger px-1 text-xs leading-none font-semibold tabular-nums text-white">
            {badge}
          </span>
        )}
      </button>
      {isOpen && <NotificationPanel onClose={() => setIsOpen(false)} />}
    </div>
  )
}

