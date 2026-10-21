'use client'

import { useEffect, useRef, useState } from 'react'
import { AlertCircle, Loader2 } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { useRedeemSignInTicketMutation, useLazyGetUserInfoQuery } from '@/store/api/identity'
import { useAppDispatch } from '@/store/hooks'
import { setUserInfo } from '@/store/slices'
import { LocalizedLink } from '@/components/ui'
import { TICKET_MESSAGE, useLocalizedRouter } from '@/hooks'
import { getApiErrorMessages, isDeploymentOrigin } from '@/lib/utils'
import { environment } from '@/config'

/** How long this page waits for its opener to hand the ticket over. */
const RECEIVE_MS = 15_000

/** A ticket the opener handed over, with the origin it came from. */
export interface ReceivedTicket {
  ticket: string
  origin: string
}

/**
 * Asks the page that opened this tab for the sign-in ticket and waits for it. Only a ticket sent by
 * that opener, from an origin `isTrustedSender` admits, is taken, so a page on any other site that
 * opened this tab cannot hand over a ticket of its own. Resolves to null when there is no opener or
 * nothing trusted answers in time; the opener is let go either way.
 */
export const receiveTicket = (
  isTrustedSender: (origin: string) => boolean,
  timeoutMs = RECEIVE_MS,
  win: Pick<Window, 'opener' | 'addEventListener' | 'removeEventListener' | 'setTimeout' | 'clearTimeout'> = window,
): Promise<ReceivedTicket | null> =>
  new Promise((resolve) => {
    const opener = win.opener as Window | null
    if (!opener) {
      resolve(null)
      return
    }

    const finish = (received: ReceivedTicket | null) => {
      win.removeEventListener('message', listen)
      win.clearTimeout(timer)
      win.opener = null
      resolve(received)
    }
    const listen = (event: MessageEvent) => {
      if (event.source !== opener || !isTrustedSender(event.origin)) return
      const data = event.data as { type?: unknown; ticket?: unknown } | null
      if (data?.type !== TICKET_MESSAGE.ticket || typeof data.ticket !== 'string' || !data.ticket.trim()) return
      finish({ ticket: data.ticket.trim(), origin: event.origin })
    }
    const timer = win.setTimeout(() => finish(null), timeoutMs)
    win.addEventListener('message', listen)
    // The request carries nothing, so it may go to whatever origin the opener is on; the opener checks
    // this tab's origin before it answers.
    opener.postMessage({ type: TICKET_MESSAGE.ready }, '*')
  })

/**
 * Whether the API serves `origin`'s host as the platform's or an existing tenant's
 * (`GET /tenants/tls-allowed`). The address template alone admits every one-label host under the
 * domain, the reserved ones too (`docs`, `status`, `mail`, ...), which a deployment may point at
 * services that run someone else's script; only a host the API answers for runs this app.
 */
export const isServedOrigin = async (origin: string, fetcher: typeof fetch = fetch): Promise<boolean> => {
  try {
    const response = await fetcher(`${environment.apiUrl}/tenants/tls-allowed?domain=${encodeURIComponent(new URL(origin).host)}`, { credentials: 'omit', cache: 'no-store' })
    return response.ok
  } catch {
    return false
  }
}

/**
 * The redemption in flight, kept outside the component: the locale guard may navigate to the tenant's
 * own default language while it runs, remounting this page, and the remounted page then waits on the
 * same redemption rather than asking for a ticket that has already been handed over.
 */
let inFlight: Promise<string | null> | null = null

/** Props for TicketSigninView. */
interface TicketSigninViewProps {
  /** The platform's address, as `GET /tenants/host` reports it; a ticket may come from there. */
  platformUrl: string | null
  /** The API's tenant address template; a ticket may come from any tenant's address it describes. */
  tenantUrlTemplate: string | null
}

/**
 * Takes the sign-in ticket from the page on another host that opened this tab, redeems it for a session
 * on this tenant's address, then lands like a password sign-in does. The ticket is single-use, so it is
 * redeemed once per page even where a development build runs effects twice. A page opened any other
 * way, or a ticket that is spent, expired or refused, shows why, with a link to the ordinary sign-in.
 */
export const TicketSigninView = ({ platformUrl, tenantUrlTemplate }: TicketSigninViewProps) => {
  const { t } = useTranslation()
  const router = useLocalizedRouter()
  const dispatch = useAppDispatch()
  const [redeem] = useRedeemSignInTicketMutation()
  const [getUserInfo] = useLazyGetUserInfoQuery()
  const [error, setError] = useState<string | null>(null)
  const started = useRef(false)

  useEffect(() => {
    if (started.current) return
    started.current = true

    const fallback = t('error.server.signInTicketInvalid') || 'This sign-in link has expired or was already used. Sign in with your password.'

    // Resolves to the landing path, or to the reason the sign-in failed.
    inFlight ??= (async () => {
      const received = await receiveTicket((origin) => isDeploymentOrigin(origin, { platformUrl, tenantUrlTemplate }))
      if (!received || !(await isServedOrigin(received.origin))) return null

      const response = await redeem({ ticket: received.ticket })
      if (response.error) throw new Error(getApiErrorMessages(response.error, t)?.messages[0] || fallback)

      const userInfo = await getUserInfo()
      if (userInfo.error || !userInfo.data) return null

      dispatch(setUserInfo(userInfo.data))
      return userInfo.data.roles.find((role) => role.name === 'Admin') ? '/admin' : '/'
    })().finally(() => {
      inFlight = null
    })

    inFlight.then(
      (landing) => {
        if (!landing) {
          setError(fallback)
          return
        }
        // A refresh after the navigation makes the root layout re-fetch the texts and languages for the
        // tenant this session now acts in, as a password sign-in does.
        router.replace(landing)
        router.refresh()
      },
      (reason: unknown) => setError(reason instanceof Error ? reason.message : fallback),
    )
  }, [dispatch, getUserInfo, platformUrl, redeem, router, t, tenantUrlTemplate])

  if (error) {
    return (
      <div className="flex flex-col items-center text-center">
        <div className="mb-5 flex size-12 items-center justify-center rounded-xl bg-surface-2 text-danger">
          <AlertCircle className="size-6" />
        </div>
        <h1 className="text-2xl font-semibold tracking-tight wrap-break-word text-foreground">{t('page.auth.ticketSignin.failedTitle') || "We couldn't sign you in automatically"}</h1>
        <p className="mt-2 text-sm wrap-break-word text-muted-foreground">{error}</p>
        <LocalizedLink href="/signin" className="btn btn-primary mt-6 w-full">
          {t('page.auth.ticketSignin.action') || 'Sign in with your password'}
        </LocalizedLink>
      </div>
    )
  }

  return (
    <div className="flex flex-col items-center text-center" role="status" aria-live="polite">
      <Loader2 className="mb-5 size-8 animate-spin text-primary" aria-hidden="true" />
      <h1 className="text-2xl font-semibold tracking-tight text-foreground">{t('page.auth.ticketSignin.title') || 'Signing you in…'}</h1>
    </div>
  )
}
