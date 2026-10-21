import { describe, expect, it, vi } from 'vitest'
import { TICKET_MESSAGE, ticketSigninUrl } from './use-open-tenant'
import { isServedOrigin, receiveTicket } from '@/app/[lang]/(auth)/ticket-signin/_components/ticket-signin-view'

describe('ticketSigninUrl', () => {
  it('is the tenant address with the ticket page, carrying no ticket and no locale', () => {
    expect(ticketSigninUrl('http://acme.localhost:3000')).toBe('http://acme.localhost:3000/ticket-signin')
  })

  it('does not double a trailing slash', () => {
    expect(ticketSigninUrl('https://acme.example.com/')).toBe('https://acme.example.com/ticket-signin')
  })
})

/** A stand-in for the page that opened the tab: records what it is asked, and is the message source. */
const fakeOpener = () => ({ postMessage: vi.fn() })

/** A window with an opener, enough for receiveTicket to listen on. */
const fakeWindow = (opener: unknown) =>
  Object.assign(new EventTarget(), {
    opener,
    setTimeout: (handler: () => void, ms: number) => setTimeout(handler, ms) as unknown as number,
    clearTimeout: (id: number) => clearTimeout(id),
  }) as unknown as Window

/** Delivers a message to the window as the browser would. */
const deliver = (win: Window, data: unknown, origin: string, source: unknown) => win.dispatchEvent(Object.assign(new Event('message'), { data, origin, source }))

const trusted = (origin: string) => origin === 'http://app.localhost:3000'

describe('receiveTicket', () => {
  it('asks the opener for the ticket and takes the one it sends from a trusted origin, then lets the opener go', async () => {
    const opener = fakeOpener()
    const win = fakeWindow(opener)

    const received = receiveTicket(trusted, 1000, win)
    expect(opener.postMessage).toHaveBeenCalledWith({ type: TICKET_MESSAGE.ready }, '*')
    deliver(win, { type: TICKET_MESSAGE.ticket, ticket: 'abc' }, 'http://app.localhost:3000', opener)

    await expect(received).resolves.toEqual({ ticket: 'abc', origin: 'http://app.localhost:3000' })
    expect(win.opener).toBeNull()
  })

  it('ignores a ticket from another origin, from a window that is not the opener, or of another kind', async () => {
    const opener = fakeOpener()
    const win = fakeWindow(opener)

    const received = receiveTicket(trusted, 50, win)
    deliver(win, { type: TICKET_MESSAGE.ticket, ticket: 'planted' }, 'https://evil.example', opener)
    deliver(win, { type: TICKET_MESSAGE.ticket, ticket: 'planted' }, 'http://app.localhost:3000', fakeOpener())
    deliver(win, { type: 'other', ticket: 'planted' }, 'http://app.localhost:3000', opener)
    deliver(win, { type: TICKET_MESSAGE.ticket, ticket: 42 }, 'http://app.localhost:3000', opener)

    await expect(received).resolves.toBeNull()
  })

  it('is null at once when nothing opened the tab', async () => {
    await expect(receiveTicket(trusted, 1000, fakeWindow(null))).resolves.toBeNull()
  })
})

describe('isServedOrigin', () => {
  it("asks the API about the sender's host, port included, and trusts only a 200", async () => {
    const fetcher = vi.fn(async () => new Response(null, { status: 200 }))
    await expect(isServedOrigin('http://acme.localhost:3000', fetcher as unknown as typeof fetch)).resolves.toBe(true)
    expect(fetcher).toHaveBeenCalledWith('/api/tenants/tls-allowed?domain=acme.localhost%3A3000', { credentials: 'omit', cache: 'no-store' })
  })

  it('refuses a host the API does not serve, and fails closed when it cannot be asked', async () => {
    await expect(isServedOrigin('https://docs.example.com', (async () => new Response(null, { status: 404 })) as unknown as typeof fetch)).resolves.toBe(false)
    await expect(isServedOrigin('https://acme.example.com', (async () => { throw new Error('down') }) as unknown as typeof fetch)).resolves.toBe(false)
  })
})
