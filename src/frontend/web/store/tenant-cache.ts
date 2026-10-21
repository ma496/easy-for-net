import { markLeavingSignedOut } from '@/store/signed-out-navigation'

/**
 * Leaves the app for the sign-in page once the session has ended - after the sign-out control and
 * after changing the password, which ends every session the account had. It is a full page load
 * rather than a client-side navigation: the new document starts with a fresh store, so no cached
 * record, tenant selection or unread badge survives for the next user of this browser. Resetting the
 * RTK Query cache in place instead would make every query on the still-mounted page refetch against
 * the dead session and show its 401 before the navigation completed. `replace` keeps the page just
 * left out of the history, so Back does not return to it. It is marked first, so no request still in
 * flight - nor the notification hub's reconnect probe - can replace this navigation with its own.
 */
export const leaveSignedOut = (signinHref: string): void => {
  markLeavingSignedOut()
  window.location.replace(signinHref)
}
