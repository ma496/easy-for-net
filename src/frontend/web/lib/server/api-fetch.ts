import { environment } from '@/config'

/**
 * The headers that tell the API which host the visitor asked for: `X-Forwarded-Host` carrying the
 * incoming request's `host` verbatim, or nothing when there is no incoming host. The API resolves the
 * tenant from that host, so a request the Next.js server makes on a visitor's behalf - which reaches the
 * API on `API_INTERNAL_URL`, not on the visitor's host - must carry it to resolve the same tenant the
 * browser's own same-origin `/api` calls do.
 */
export function forwardedHostHeaders(host: string | null | undefined): Record<string, string> {
  return host ? { 'X-Forwarded-Host': host } : {}
}

/** Options for `serverApiFetch`: a plain `RequestInit`, plus whether to forward the visitor's cookies. */
export type ServerApiFetchInit = Omit<RequestInit, 'headers'> & {
  headers?: Record<string, string>
  /** Forward the incoming `cookie` header, so the API sees the visitor's own session. */
  forwardCookies?: boolean
}

/**
 * The one way the Next.js server calls the API: `path` (starting with `/`) on `API_INTERNAL_URL`, with
 * the incoming request's host forwarded (see `forwardedHostHeaders`) and, when asked, its cookies.
 *
 * `next/headers` is loaded dynamically rather than imported at the top of the file, for the reason
 * `i18n/server.ts` gives: that module, which calls this, is re-exported from the `@/i18n` barrel client
 * components import, and a static import of a server-only API would put it in their module graph. The
 * `headers()` call also comes before `API_INTERNAL_URL` is read, so a prerender attempt stops on Next's
 * dynamic-usage signal first and `next build` never needs the variable.
 */
export async function serverApiFetch(path: string, { headers: extraHeaders, forwardCookies = false, ...init }: ServerApiFetchInit = {}): Promise<Response> {
  const { headers } = await import('next/headers')
  const headerList = await headers()
  const cookie = forwardCookies ? headerList.get('cookie') : null

  return fetch(`${environment.serverApiUrl}${path}`, {
    ...init,
    headers: {
      ...extraHeaders,
      ...forwardedHostHeaders(headerList.get('host')),
      ...(cookie ? { cookie } : {}),
    },
  })
}
