/** Build-time environment configuration exposing the API base URL and convenience production/development flags. */
export const environment = {
  /**
   * The API base URL the browser uses. It defaults to `/api` on the web app's own origin, where the
   * development proxy and the production reverse proxy both route `/api` (and `/hubs`) to the API, so the
   * auth cookie stays first-party and the tenant's host reaches the API unchanged. `NEXT_PUBLIC_API_URL`
   * (inlined at build time) overrides it.
   */
  apiUrl: process.env.NEXT_PUBLIC_API_URL || '/api',
  /**
   * The API base URL for requests the Next.js server makes itself - it cannot use the browser's relative
   * `/api`, so `API_INTERNAL_URL` (read at run time, never inlined) must point at the API directly. Server
   * only: it is a getter so that importing this module - client code does - never reads it, and reading it
   * with the variable unset throws naming it rather than fetching from a wrong address.
   */
  get serverApiUrl(): string {
    const url = process.env.API_INTERNAL_URL
    if (!url) {
      throw new MissingServerApiUrlError()
    }
    return url
  },
  isProduction: process.env.NEXT_PUBLIC_APP_ENV === 'production',
  isDevelopment: process.env.NEXT_PUBLIC_APP_ENV === 'development',
}

/** Thrown when the server needs the API's address and `API_INTERNAL_URL` is not set. */
export class MissingServerApiUrlError extends Error {
  constructor() {
    super('API_INTERNAL_URL is not set: the Next.js server needs the API base URL it fetches from (e.g. http://localhost:5000/api in development, http://api:8080/api in a container).')
    this.name = 'MissingServerApiUrlError'
  }
}
