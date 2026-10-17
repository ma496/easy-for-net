'use client'

import { useCallback } from 'react'
import { useSignInTicketMutation } from '@/store/api/identity'
import { apiErrorAlert } from '@/lib/utils'

/** The path, on the tenant's own address, that redeems a sign-in ticket. */
export const TICKET_SIGNIN_PATH = '/ticket-signin'

/** The messages the opening page and the tenant's `ticket-signin` page exchange. */
export const TICKET_MESSAGE = {
  /** Sent by the tenant's page to its opener once it is listening; it carries nothing. */
  ready: 'efn:sign-in-ticket:ready',
  /** Sent by the opener back to that page alone, carrying the ticket. */
  ticket: 'efn:sign-in-ticket',
} as const

/** How long the opening page waits for the tenant's page to ask for the ticket: the ticket's own lifetime. */
const HANDOFF_MS = 60_000

/**
 * The tenant's `ticket-signin` page. No ticket is ever written into the address and no locale either -
 * the tenant's host negotiates its own.
 */
export const ticketSigninUrl = (tenantUrl: string): string => `${tenantUrl.replace(/\/+$/, '')}${TICKET_SIGNIN_PATH}`

/**
 * Opens one of the caller's tenants at its own address in a new tab, already signed in. Each host keeps
 * its own session, so the API issues a single-use ticket here and the tenant's host exchanges it for a
 * session of its own.
 *
 * The ticket never travels in a URL. It is handed over with `postMessage`, to the tab this page opened
 * and only once that tab is on the tenant's origin, so a ticket cannot be planted in a link: a link
 * carrying someone else's ticket - which would sign a victim in to the sender's account - has nothing
 * to carry. The tab is opened before the ticket is asked for, while the click still counts as a user
 * gesture - a tab opened after an `await` is blocked as a pop-up - and is closed again when the API
 * refuses.
 */
export const useOpenTenant = () => {
  const [issueTicket, { isLoading }] = useSignInTicketMutation()

  const openTenant = useCallback(
    async (tenantId: string) => {
      // The opener is kept: the tenant's page asks it for the ticket.
      const tab = window.open('', '_blank')

      const response = await issueTicket({ tenantId })
      if (response.error || !response.data) {
        tab?.close()
        if (response.error) apiErrorAlert(response.error)
        return
      }

      const { tenantUrl, ticket } = response.data
      if (!tab) {
        // The browser refused the tab: open the tenant here, to sign in with a password there.
        window.location.href = tenantUrl
        return
      }

      const tenantOrigin = new URL(tenantUrl).origin
      const stop = () => {
        window.removeEventListener('message', deliver)
        window.clearTimeout(timer)
      }
      const deliver = (event: MessageEvent) => {
        if (event.source !== tab || event.origin !== tenantOrigin || event.data?.type !== TICKET_MESSAGE.ready) return
        stop()
        tab.postMessage({ type: TICKET_MESSAGE.ticket, ticket }, tenantOrigin)
      }
      const timer = window.setTimeout(stop, HANDOFF_MS)
      window.addEventListener('message', deliver)

      tab.location.href = ticketSigninUrl(tenantUrl)
    },
    [issueTicket],
  )

  return { openTenant, isOpening: isLoading }
}
