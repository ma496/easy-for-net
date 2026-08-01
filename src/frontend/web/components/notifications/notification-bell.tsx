'use client'
import { useState, useRef, useEffect } from 'react'
import { Bell } from 'lucide-react'
import { useAppSelector } from '@/store/hooks'
import { NotificationPanel } from './notification-panel'

/**
 * Header bell button that toggles the {@link NotificationPanel} and shows an unread-count badge (capped at "99+") from the notifications slice.
 */
export const NotificationBell = () => {
  const [isOpen, setIsOpen] = useState(false)
  const unreadCount = useAppSelector(state => state.notifications.unreadCount)
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
        onClick={() => setIsOpen(!isOpen)}
      >
        <Bell className="h-5 w-5" />
        {unreadCount > 0 && (
          <span className="absolute -top-1 ltr:-right-1 rtl:-left-1 flex h-5 w-5 items-center justify-center rounded-full bg-danger text-xs text-white">
            {unreadCount > 99 ? '99+' : unreadCount}
          </span>
        )}
      </button>
      {isOpen && <NotificationPanel onClose={() => setIsOpen(false)} />}
    </div>
  )
}

