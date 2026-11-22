/**
 * Where a page opened on `next dev`'s own port should be sent instead, or `null` when it is fine.
 *
 * In development the browser is meant to open the dev proxy (`npm run dev` starts it on :3000), which
 * routes `/api` and `/hubs` to the API and everything else to `next dev` on :3001. `next dev` prints its
 * own address too, and a page opened there renders but cannot reach the API: `/api/*` answers Next's
 * HTML 404, which a sign-in then fails to parse as JSON. `npm run dev` names the proxy's port to
 * `next dev` as `DEV_PROXY_PORT`, so the web app can send such a visitor to the same page through the
 * proxy instead.
 *
 * The decision reads the `Host` header, which the dev proxy passes through untouched: through the
 * proxy it names the proxy's port, opened directly it names `next dev`'s. The hostname is kept, so
 * `default.localhost:3001` goes to `default.localhost:3000`, not to some other host.
 *
 * @param url The request's URL; its path and query are carried over.
 * @param host The request's `Host` header.
 * @param proxyPort The dev proxy's port, or nothing when no dev proxy fronts this server.
 */
export function devProxyRedirect(url: URL, host: string | null, proxyPort: string | undefined): URL | null {
  if (!proxyPort || !host) return null
  // Parsed as a URL so a bracketed IPv6 host (`[::1]:3001`) splits into hostname and port correctly.
  let requested: URL
  try {
    requested = new URL(`http://${host}`)
  } catch {
    return null
  }
  if (requested.port === proxyPort) return null
  const target = new URL(url.pathname + url.search, `http://${requested.hostname}:${proxyPort}`)
  return target
}
