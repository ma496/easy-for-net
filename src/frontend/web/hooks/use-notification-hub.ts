'use client'
import { useEffect } from 'react'
import { useAppDispatch, useAppSelector } from '@/store/hooks'
import { setUnreadCount } from '@/store/slices'
import { useNotificationGetUnreadCountQuery } from '@/store/api/notifications'
import { isPlatformWithoutTenant } from '@/lib/utils'

const POLL_INTERVAL_MS = 30_000

/**
 * Polls the notifications API at a fixed interval for the unread count and
 * syncs the result into the notifications Redux slice. Designed to be
 * mounted once (e.g. in the app shell) to keep the badge counter live.
 * Notifications are read in the tenant being acted in, or in platform scope by a
 * platform administrator acting in none, so nothing is polled for anybody else
 * without an active tenant - they would only be refused on every poll.
 */
export function useNotificationHub() {
  const dispatch = useAppDispatch()
  const canReadNotifications = useAppSelector((state) => state.auth.activeTenant != null || isPlatformWithoutTenant(state.auth.user))
  const { data } = useNotificationGetUnreadCountQuery({}, {
    pollingInterval: POLL_INTERVAL_MS,
    skip: !canReadNotifications,
  })

  useEffect(() => {
    if (data?.count !== undefined) {
      dispatch(setUnreadCount(data.count))
    }
  }, [data, dispatch])
}
