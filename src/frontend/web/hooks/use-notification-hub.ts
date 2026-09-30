'use client'
import { useEffect } from 'react'
import { HttpTransportType, HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { environment } from '@/config'
import { useAppDispatch, useAppSelector } from '@/store/hooks'
import { notificationReceived, setUnreadCount } from '@/store/slices'
import {
  NOTIFICATIONS_LIST_TAG,
  notificationsApi,
  useNotificationGetUnreadCountQuery,
  type NotificationReceivedMessage,
  type UnreadCountChangedMessage,
} from '@/store/api/notifications'
import { isPlatformWithoutTenant } from '@/lib/utils'
import { fallbackPollDelayMs, reconnectDelayMs } from '@/lib/notifications/hub-reconnect'
import { notificationHubUrl } from '@/lib/notifications/hub-url'
import { isLeavingSignedOut } from '@/store/signed-out-navigation'

/** The client methods the notification hub invokes - the whole of its server-to-client protocol. */
const HubMethods = {
  notificationReceived: 'notificationReceived',
  unreadCountChanged: 'unreadCountChanged',
} as const

/**
 * Keeps `notificationsSlice.unreadCount` - the header bell's badge - live, and every notification list
 * fresh, over the API's SignalR notification hub, falling back to polling only while the hub is
 * unreachable. Mounted once, in the app shell; never per screen.
 *
 * **Who connects.** Notifications are read in the tenant being acted in, or in platform scope by a
 * platform account acting in none. An ordinary account left with no tenant (its tenant suspended or
 * left, and dropped when the session was renewed) is sent to choose one, so nothing connects or polls
 * until it has.
 *
 * **Lifecycle.** One connection to `/hubs/notifications` on the API host (outside the API route
 * prefix), WebSockets only with negotiation skipped, authenticated by the same auth cookie every API
 * request carries - no access token in the URL. It is opened while that rule holds and stopped when it
 * stops holding, on sign-out (which clears the user), and on unmount. The account and the acting tenant
 * are part of the connection's identity: a tenant switch or exit stops it and opens a new one, because
 * the hub puts a connection into its scope's groups only when it connects.
 *
 * **Messages.** `notificationReceived` counts one more unread and invalidates the notification lists
 * (`NOTIFICATIONS_LIST_TAG`) but not the unread count, which the push has just counted.
 * `unreadCountChanged` - the caller's read state changed, in this tab or another - replaces the count.
 * After every connect and every reconnect the count is fetched once, since pushes made while the
 * connection was down are gone.
 *
 * **Reconnecting.** SignalR's automatic reconnect with `reconnectDelayMs`: exponential back-off with
 * jitter, capped at 60 s, never giving up. Automatic reconnect covers only a connection that was once
 * up, so a start that fails - and a close the server does not allow to reconnect - is retried by this
 * hook on the same schedule.
 *
 * **401.** A refused WebSocket upgrade surfaces in the browser as a bare connection error: with
 * negotiation skipped there is no HTTP response whose status script can read, so a 401 cannot be told
 * apart from the API being down. So every failed attempt to connect - a start, or one of automatic
 * reconnect's attempts - is treated as "maybe 401", and answered by one unread-count fetch through RTK
 * Query, on the back-off schedule itself. That request carries the same cookie the upgrade did, and the
 * hub refuses exactly when an HTTP request with the same credential would, so it answers 401 exactly when
 * the upgrade was refused for authentication - and then `baseQueryWithReauth` refreshes the session under
 * its mutex, serialized with every other request's refresh, or signs out and redirects to sign-in when
 * the refresh fails (which stops this connection). The next back-off attempt then connects with the
 * renewed cookie, however long the outage has lasted. The same fetch is the count the badge needs while
 * disconnected, and it runs no oftener than the back-off - nothing here ever retries hot.
 * A connection merely being lost is not probed, only a failed attempt. Sign-out ends the session and the
 * server closes the connection at once, so a probe then would find no session to refresh; nothing is
 * probed once `leaveSignedOut` has marked the navigation to sign-in (`isLeavingSignedOut`), and
 * `baseQueryWithReauth` would not redirect over it in any case.
 *
 * **Fallback polling.** None while connected. While disconnected, the unread count is fetched every
 * `fallbackPollDelayMs` (60 s, give or take 10%) - but not while the tab is hidden; when it becomes
 * visible again the count is fetched once and the poll resumes.
 */
export function useNotificationHub() {
  const dispatch = useAppDispatch()
  const canReadNotifications = useAppSelector((state) => state.auth.activeTenant != null || isPlatformWithoutTenant(state.auth.user))
  const userId = useAppSelector((state) => state.auth.user?.id)
  const activeTenantId = useAppSelector((state) => state.auth.activeTenant?.id)

  // Subscribed, not polled: it gives the badge its first count, is refetched by every notification
  // mutation (they invalidate the whole `Notifications` type), and shares its cache entry with the
  // forced fetches below, so all of them reach the slice through the one effect that follows.
  const { data, fulfilledTimeStamp } = useNotificationGetUnreadCountQuery({}, { skip: !canReadNotifications })

  useEffect(() => {
    if (data?.count !== undefined) {
      dispatch(setUnreadCount(data.count))
    }
    // The timestamp too: a refetch that answers the same count keeps the same `data` object, yet the
    // slice may have counted pushes past it since.
  }, [data, fulfilledTimeStamp, dispatch])

  useEffect(() => {
    if (!canReadNotifications || typeof window === 'undefined') return

    let disposed = false
    // Whether the hub is currently unreachable - between a lost connection or failed start and the next
    // successful connect. The fallback poll runs only then.
    let outage = false
    let failedStarts = 0
    let startTimer: ReturnType<typeof setTimeout> | undefined
    let pollTimer: ReturnType<typeof setTimeout> | undefined

    // Through RTK Query, so through baseQueryWithReauth: see "401" above.
    const fetchUnreadCount = () => {
      if (disposed || isLeavingSignedOut()) return
      void dispatch(notificationsApi.endpoints.notificationGetUnreadCount.initiate({}, { forceRefetch: true, subscribe: false }))
    }

    const stopPolling = () => {
      clearTimeout(pollTimer)
      pollTimer = undefined
    }

    const schedulePoll = () => {
      stopPolling()
      if (disposed || !outage || document.visibilityState === 'hidden') return
      pollTimer = setTimeout(() => {
        pollTimer = undefined
        fetchUnreadCount()
        schedulePoll()
      }, fallbackPollDelayMs())
    }

    // Only marks the outage: a lost connection is probed once an attempt to reconnect has failed (see "401").
    const enterOutage = () => {
      if (disposed || outage) return
      outage = true
      schedulePoll()
    }

    const connected = () => {
      if (disposed) return
      outage = false
      failedStarts = 0
      stopPolling()
      fetchUnreadCount()
    }

    const onVisibilityChange = () => {
      if (!outage) return
      if (document.visibilityState === 'visible') {
        fetchUnreadCount()
        schedulePoll()
      } else {
        stopPolling()
      }
    }

    const connection = new HubConnectionBuilder()
      .withUrl(notificationHubUrl(environment.apiUrl, window.location.origin), {
        transport: HttpTransportType.WebSockets,
        skipNegotiation: true,
        withCredentials: true,
      })
      .withAutomaticReconnect({
        // Called when the connection is lost (count 0) and after each failed attempt, which is probed.
        nextRetryDelayInMilliseconds: ({ previousRetryCount }) => {
          if (previousRetryCount > 0) fetchUnreadCount()
          return reconnectDelayMs(previousRetryCount)
        },
      })
      // Every failed attempt is expected and handled; outside development the console stays quiet.
      .configureLogging(environment.isDevelopment ? LogLevel.Warning : LogLevel.None)
      .build()

    const start = async () => {
      startTimer = undefined
      if (disposed) return
      try {
        await connection.start()
      } catch {
        if (disposed) return
        enterOutage()
        fetchUnreadCount()
        scheduleStart()
        return
      }
      if (disposed) {
        void connection.stop().catch(() => undefined)
        return
      }
      connected()
    }

    const scheduleStart = () => {
      if (disposed || startTimer !== undefined) return
      startTimer = setTimeout(() => void start(), reconnectDelayMs(failedStarts++))
    }

    connection.on(HubMethods.notificationReceived, (_message: NotificationReceivedMessage) => {
      dispatch(notificationReceived())
      dispatch(notificationsApi.util.invalidateTags([NOTIFICATIONS_LIST_TAG]))
    })
    connection.on(HubMethods.unreadCountChanged, (message: UnreadCountChangedMessage) => {
      if (typeof message?.count === 'number') {
        dispatch(setUnreadCount(message.count))
      }
    })
    connection.onreconnecting(() => enterOutage())
    connection.onreconnected(() => connected())
    // Reached only when automatic reconnect does not apply - the server closed the connection without
    // allowing it - since the retry policy never gives up. Start again, on the same back-off.
    connection.onclose(() => {
      if (disposed) return
      enterOutage()
      scheduleStart()
    })

    document.addEventListener('visibilitychange', onVisibilityChange)
    void start()

    return () => {
      disposed = true
      clearTimeout(startTimer)
      stopPolling()
      document.removeEventListener('visibilitychange', onVisibilityChange)
      void connection.stop().catch(() => undefined)
    }
    // The account and the acting tenant name the connection's groups, so either changing reconnects.
  }, [canReadNotifications, userId, activeTenantId, dispatch])
}
