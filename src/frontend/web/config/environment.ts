/** Build-time environment configuration exposing the API base URL and convenience production/development flags. */
export const environment = {
  apiUrl: process.env.NEXT_PUBLIC_API_URL,
  /**
   * The API base URL for requests the Next.js server makes itself. Inside a container the public URL
   * the browser uses may not resolve, so `API_INTERNAL_URL` (read at run time, never inlined) can point
   * at the API directly; without it the server uses the public URL.
   */
  serverApiUrl: process.env.API_INTERNAL_URL || process.env.NEXT_PUBLIC_API_URL,
  isProduction: process.env.NEXT_PUBLIC_APP_ENV === 'production',
  isDevelopment: process.env.NEXT_PUBLIC_APP_ENV === 'development',
}
