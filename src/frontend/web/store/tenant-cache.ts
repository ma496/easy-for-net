import { appApi } from '@/store/api/_app-api'
import { GetUserInfoResponse } from '@/store/api/identity'
import { setUnreadCount, setUserInfo } from '@/store/slices'

/** One of the actions a tenant cache-reset sequence dispatches, in the order its builder returns them. */
export type TenantCacheAction =
  | ReturnType<typeof appApi.util.resetApiState>
  | ReturnType<typeof setUserInfo>
  | ReturnType<typeof setUnreadCount>

/**
 * Actions to dispatch, in order, once the active tenant has changed - on a switch from the header,
 * the chooser or the tenants table. The RTK Query cache is discarded first, so no record cached for the previous
 * tenant is displayed afterwards; the freshly read user info then replaces the session state
 * and the unread notification badge starts again from zero for the new tenant.
 */
export const tenantChangedActions = (userInfo: GetUserInfoResponse | undefined): TenantCacheAction[] => [
  appApi.util.resetApiState(),
  setUserInfo(userInfo),
  setUnreadCount(0),
]

/**
 * The part of a store the sequences above are dispatched through. Only `dispatch`
 * is asked for, so a caller - a component, a listener, a test - can hand in the
 * store's own dispatch without the module having to know how the store is built.
 */
export type TenantCacheDispatcher = (action: TenantCacheAction) => unknown

/**
 * Dispatches the tenant-changed sequence, so every screen that changes the active
 * tenant drops the previous tenant's cached records through one call rather than
 * repeating the sequence: the switcher, the chooser and the tenants table all
 * reach the reset only this way.
 */
export const dispatchTenantChanged = (
  dispatch: TenantCacheDispatcher,
  userInfo: GetUserInfoResponse | undefined
): void => {
  tenantChangedActions(userInfo).forEach((action) => dispatch(action))
}

/**
 * Leaves the app for the sign-in page once the session has ended - after the sign-out control and
 * after changing the password, which ends every session the account had. It is a full page load
 * rather than a client-side navigation: the new document starts with a fresh store, so no cached
 * record, tenant selection or unread badge survives for the next user of this browser. Resetting the
 * RTK Query cache in place instead would make every query on the still-mounted page refetch against
 * the dead session and show its 401 before the navigation completed. `replace` keeps the page just
 * left out of the history, so Back does not return to it.
 */
export const leaveSignedOut = (signinHref: string): void => {
  window.location.replace(signinHref)
}
