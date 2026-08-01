import { NotificationType } from './enums'
import { BaseDto, RequestBase, GenericAuditableDto, ListRequestDto, ListDto } from '@/store/api'

/** Request body for creating a notification, using i18n keys for title/message and optional group/metadata. */
export interface NotificationCreateRequest extends RequestBase {
  type: NotificationType
  titleKey: string
  messageKey: string
  group?: string
  metadata?: string
}

/** Response from the create-notification endpoint, returning the new notification's id, type, i18n keys, and read state. */
export interface NotificationCreateResponse extends BaseDto<string> {
  type: NotificationType
  titleKey: string
  messageKey: string
  isRead: boolean
  group?: string
  metadata?: string
}

/** Request parameters for deleting a notification by id. */
export interface NotificationDeleteRequest extends BaseDto<string>, RequestBase { }

/** Response from the delete-notification endpoint, indicating success and an accompanying message. */
export interface NotificationDeleteResponse extends BaseDto<string> {
  success: boolean
  message: string
}

/** Request parameters for fetching a single notification by id. */
export interface NotificationGetRequest extends BaseDto<string>, RequestBase { }

/** Response from the get-notification endpoint, returning the notification content, read state, group, metadata, and audit fields. */
export interface NotificationGetResponse extends GenericAuditableDto<string> {
  type: NotificationType
  titleKey: string
  messageKey: string
  isRead: boolean
  group?: string
  metadata?: string
}

/** Request parameters for the list-notifications endpoint, extending the standard list options with isRead and group filters. */
export interface NotificationListRequest extends ListRequestDto<string>, RequestBase {
  isRead?: boolean | null
  group?: string
}

/** Paged response of notifications returned by the list-notifications endpoint. */
export interface NotificationListResponse extends ListDto<NotificationListDto> { }

/** Summary representation of a notification in list responses. */
export interface NotificationListDto extends GenericAuditableDto<string> {
  type: NotificationType
  titleKey: string
  messageKey: string
  isRead: boolean
  group?: string
  metadata?: string
}

/** Alias for the list/standard notification DTO, used as a generic notification type. */
export type NotificationDto = NotificationListDto

/** Request body for marking a single notification as read by id. */
export interface NotificationMarkAsReadRequest extends BaseDto<string>, RequestBase { }

/** Response from the mark-as-read endpoint, indicating success and an accompanying message. */
export interface NotificationMarkAsReadResponse extends BaseDto<string> {
  success: boolean
  message: string
}

/** Request body for marking a single notification as unread by id. */
export interface NotificationMarkAsUnreadRequest extends BaseDto<string>, RequestBase { }

/** Response from the mark-as-unread endpoint, indicating success and an accompanying message. */
export interface NotificationMarkAsUnreadResponse extends BaseDto<string> {
  success: boolean
  message: string
}

/** Request body for the mark-all-as-read endpoint (currently empty). */
export interface NotificationMarkAllAsReadRequest extends RequestBase { }

/** Response from the mark-all-as-read endpoint, indicating success and an accompanying message. */
export interface NotificationMarkAllAsReadResponse extends BaseDto<string> {
  success: boolean
  message: string
}

/** Request parameters for the unread-count endpoint (currently empty). */
export interface NotificationGetUnreadCountRequest extends RequestBase { }

/** Response from the unread-count endpoint, containing the total number of unread notifications. */
export interface NotificationGetUnreadCountResponse {
  count: number
}

/** Request parameters for the groups endpoint (currently empty). */
export interface NotificationGetGroupsRequest extends RequestBase { }

/** Response from the groups endpoint, returning the list of distinct notification group names. */
export interface NotificationGetGroupsResponse {
  groups: string[]
}
