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
        className="icon-btn"
        aria-expanded={isOpen}
        onClick={() => setIsOpen(!isOpen)}
      >
        <span className="sr-only">{t('common.notifications')}</span>
        {/* The badge is anchored to the glyph, not the button, so it sits on the bell's shoulder */}
        <span className="relative inline-flex">
          <Bell size={18} aria-hidden="true" />
          {badge !== null && (
            <span className="absolute -top-2 inset-s-3 flex h-4 min-w-4 items-center justify-center rounded-full bg-danger px-1 text-[10px] leading-none font-semibold tabular-nums text-danger-foreground ring-2 ring-background">
              {badge}
            </span>
          )}
        </span>
      </button>
      {isOpen && <NotificationPanel onClose={() => setIsOpen(false)} />}
    </div>
  )
}

