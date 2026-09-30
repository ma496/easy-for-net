import { appApi } from '@/store/api/_app-api'
import {
  NotificationCreateRequest,
  NotificationCreateResponse,
  NotificationDeleteRequest,
  NotificationDeleteResponse,
  NotificationGetRequest,
  NotificationGetResponse,
  NotificationListRequest,
  NotificationListResponse,
  NotificationMarkAsReadResponse,
  NotificationMarkAsUnreadResponse,
  NotificationMarkAllAsReadRequest,
  NotificationMarkAllAsReadResponse,
  NotificationGetUnreadCountRequest,
  NotificationGetUnreadCountResponse,
  NotificationGetGroupsRequest,
  NotificationGetGroupsResponse,
} from './notifications-dtos'

/**
 * The tag every notification list (and the group list) provides, and the one the hub's
 * `notificationReceived` push invalidates. Invalidating the bare `Notifications` type would refetch the
 * unread count as well, which the push has already counted.
 */
export const NOTIFICATIONS_LIST_TAG = { type: 'Notifications' as const, id: 'LIST' }

/**
 * RTK Query API for notifications: CRUD endpoints, listing with filters
 * (read state, group), mark-as-read / mark-as-unread for individual
 * notifications, mark-all-as-read, unread count, and grouped listing.
 * Uses the 'Notifications' tag type for cache invalidation.
 */
export const notificationsApi = appApi
  .enhanceEndpoints({
    addTagTypes: ['Notifications'],
  })
  .injectEndpoints({
    overrideExisting: false,
    endpoints: (builder) => ({
      notificationCreate: builder.mutation<NotificationCreateResponse, NotificationCreateRequest>({
        query: (input) => ({
          url: '/notifications',
          method: 'POST',
          body: input,
        }),
        invalidatesTags: ['Notifications'],
      }),
      notificationDelete: builder.mutation<NotificationDeleteResponse, NotificationDeleteRequest>({
        query: (input) => ({
          url: `/notifications/${input.id}`,
          method: 'DELETE',
        }),
        invalidatesTags: (result, error, arg) => ['Notifications', { type: 'Notifications', id: arg.id }],
      }),
      notificationGet: builder.query<NotificationGetResponse, NotificationGetRequest>({
        query: (input) => ({
          url: `/notifications/${input.id}`,
          method: 'GET',
        }),
        providesTags: (result, error, arg) => [{ type: 'Notifications', id: arg.id }],
      }),
      notificationList: builder.query<NotificationListResponse, NotificationListRequest>({
        query: ({ page, pageSize, sortField, sortDirection, search, all, includeIds, isRead, group }) => ({
          url: '/notifications',
          params: {
            page,
            pageSize,
            sortField,
            sortDirection,
            search,
            all,
            includeIds,
            ...(isRead !== null && isRead !== undefined ? { isRead } : {}),
            ...(group ? { group } : {}),
          },
          method: 'GET',
        }),
        // The LIST id lets a hub push refresh every list without also refetching the unread count, which
        // the push has already counted; the mutations above still reach it through the bare type.
        providesTags: (result) => [
          'Notifications',
          NOTIFICATIONS_LIST_TAG,
          ...(result?.items?.map((item) => ({ type: 'Notifications' as const, id: item.id })) ?? []),
        ],
      }),
      notificationMarkAsRead: builder.mutation<NotificationMarkAsReadResponse, { id: string }>({
        query: (input) => ({
          url: `/notifications/${input.id}/mark-as-read`,
          method: 'POST',
          body: input,
        }),
        invalidatesTags: (result, error, arg) => ['Notifications', { type: 'Notifications', id: arg.id }],
      }),
      notificationMarkAsUnread: builder.mutation<NotificationMarkAsUnreadResponse, { id: string }>({
        query: (input) => ({
          url: `/notifications/${input.id}/mark-as-unread`,
          method: 'POST',
          body: input,
        }),
        invalidatesTags: (result, error, arg) => ['Notifications', { type: 'Notifications', id: arg.id }],
      }),
      notificationMarkAllAsRead: builder.mutation<NotificationMarkAllAsReadResponse, NotificationMarkAllAsReadRequest>({
        query: () => ({
          url: '/notifications/mark-all-as-read',
          method: 'POST',
        }),
        invalidatesTags: ['Notifications'],
      }),
      notificationGetUnreadCount: builder.query<NotificationGetUnreadCountResponse, NotificationGetUnreadCountRequest>({
        query: () => ({
          url: '/notifications/unread-count',
          method: 'GET',
        }),
        // Tagged so that every mutation above - each invalidates the whole 'Notifications' type - refetches
        // the badge at once instead of leaving it stale until the hub or the fallback poll reports it.
        providesTags: [{ type: 'Notifications', id: 'UNREAD_COUNT' }],
      }),
      notificationGetGroups: builder.query<NotificationGetGroupsResponse, NotificationGetGroupsRequest>({
        query: () => ({
          url: '/notifications/groups',
          method: 'GET',
        }),
        // A pushed notification may bring a group nobody has seen yet.
        providesTags: ['Notifications', NOTIFICATIONS_LIST_TAG],
      }),
    }),
  })

export const {
  useNotificationCreateMutation,
  useNotificationDeleteMutation,
  useNotificationGetQuery,
  useLazyNotificationGetQuery,
  useNotificationListQuery,
  useLazyNotificationListQuery,
  useNotificationMarkAsReadMutation,
  useNotificationMarkAsUnreadMutation,
  useNotificationMarkAllAsReadMutation,
  useNotificationGetUnreadCountQuery,
  useNotificationGetGroupsQuery
} = notificationsApi
