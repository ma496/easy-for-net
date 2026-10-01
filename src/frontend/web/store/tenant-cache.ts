import { markLeavingSignedOut } from '@/store/signed-out-navigation'

/** The success message to show once the page loaded by {@link leaveForTenantChange} has rendered. */
export interface TenantChangeNotice {
  /** Translation key of the message, resolved after the load so it is read in the new scope's language. */
  messageKey: string
  /** The tenant entered, interpolated as `${tenant}`; empty when the session left for platform scope. */
  tenant: string
}

/** Where {@link TenantChangeNotice} waits for the next document; session storage, so it never outlives the tab. */
const tenantChangeNoticeKey = 'tenant-change-notice'

/**
 * Lands on `href` once the acting tenant has changed - on a switch from the header, the chooser or the
 * tenants table, and on leaving a tenant for platform scope. It is a full page load rather than a
 * client-side navigation, for the same reason {@link leaveSignedOut} is: the new document starts with a
 * fresh store, so no record cached for the scope just left can be displayed, and the session state, the
 * unread badge, the notification hub and the root layout's texts and languages are all read again for
 * the scope just entered. Resetting the RTK Query cache in place instead makes every query on the
 * still-mounted page refetch under the new session, and a page the new scope may not open (the tenants
 * table inside a tenant, a tenant's users in platform scope) shows its 403 until the navigation lands.
 * `replace` keeps that page out of the history too, so Back does not return to it in the wrong scope.
 */
export const leaveForTenantChange = (href: string, notice: TenantChangeNotice): void => {
  try {
    window.sessionStorage.setItem(tenantChangeNoticeKey, JSON.stringify(notice))
  } catch {
    // Storage refused (a private window, blocked site data): the change still lands, only unannounced.
  }
  window.location.replace(href)
}

/** Reads and removes the notice {@link leaveForTenantChange} left for this document, so it is shown once. */
export const takeTenantChangeNotice = (): TenantChangeNotice | null => {
  try {
    const stored = window.sessionStorage.getItem(tenantChangeNoticeKey)
    if (stored === null) return null
    window.sessionStorage.removeItem(tenantChangeNoticeKey)
    const notice: unknown = JSON.parse(stored)
    if (
      typeof notice === 'object' &&
      notice !== null &&
      typeof (notice as TenantChangeNotice).messageKey === 'string' &&
      typeof (notice as TenantChangeNotice).tenant === 'string'
    ) {
      return notice as TenantChangeNotice
    }
  } catch {
    // Unreadable or malformed: nothing to announce.
  }
  return null
}

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
