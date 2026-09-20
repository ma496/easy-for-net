import { appApi } from '@/store/api/_app-api'
import { GetUserInfoResponse } from '@/store/api/identity'
import { setUnreadCount, setUserInfo, signout } from '@/store/slices'

/** One of the actions a tenant cache-reset sequence dispatches, in the order its builder returns them. */
export type TenantCacheAction =
  | ReturnType<typeof appApi.util.resetApiState>
  | ReturnType<typeof setUserInfo>
  | ReturnType<typeof setUnreadCount>
  | ReturnType<typeof signout>

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
 * Actions to dispatch, in order, when the user signs out. The tenant-scoped data cached in the
 * browser is discarded first, the stored active-tenant selection is cleared with the rest of the
 * auth state and the unread notification badge - which lives in slice state and so survives the
 * cache reset - goes back to zero, so the next user signing in on this browser inherits neither a
 * tenant selection nor a previous tenant's records.
 */
export const signedOutActions = (): TenantCacheAction[] => [
  appApi.util.resetApiState(),
  signout(),
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
 * Dispatches the signed-out sequence. Every way out of the session goes through
 * it - the sign-out control and changing the password,
 * which ends every session the account had - so no path out of the app can leave
 * a previous tenant's records behind for the next user of this browser.
 */
export const dispatchSignedOut = (dispatch: TenantCacheDispatcher): void => {
  signedOutActions().forEach((action) => dispatch(action))
}
