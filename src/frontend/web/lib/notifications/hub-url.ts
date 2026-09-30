/** The notification hub's path on the API host. It is mapped outside the API's route prefix. */
export const NOTIFICATION_HUB_PATH = '/hubs/notifications'

/**
 * The absolute URL of the notification hub, built from the configured API base URL. The hub is mapped at
 * the root of the API host rather than under its route prefix, so everything after the host in the API
 * URL (`/api`, say) is dropped. An API URL that is itself relative (served from the web app's own origin
 * behind a proxy) resolves against `origin`; one that is missing resolves to `origin` itself.
 */
export function notificationHubUrl(apiUrl: string | undefined, origin: string): string {
  const apiBase = new URL(apiUrl || '/', origin)
  return new URL(NOTIFICATION_HUB_PATH, apiBase).toString()
}
