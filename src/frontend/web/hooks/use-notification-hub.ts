'use client'
import { useEffect } from 'react'
import { useAppDispatch } from '@/store/hooks'
import { setUnreadCount } from '@/store/slices/notificationsSlice'
import { useNotificationGetUnreadCountQuery } from '@/store/api/notifications/notifications/notifications-api'

const POLL_INTERVAL_MS = 30_000

/**
 * Polls the notifications API at a fixed interval for the unread count and
 * syncs the result into the notifications Redux slice. Designed to be
 * mounted once (e.g. in the app shell) to keep the badge counter live.
 */
export function useNotificationHub() {
  const dispatch = useAppDispatch()
  const { data } = useNotificationGetUnreadCountQuery({}, {
    pollingInterval: POLL_INTERVAL_MS,
  })

  useEffect(() => {
    if (data?.count !== undefined) {
      dispatch(setUnreadCount(data.count))
    }
  }, [data, dispatch])
}
