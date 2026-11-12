/**
 * Timing for the notification hub connection: how long to wait before the next connection attempt, and
 * how often to poll the unread count while there is no connection. Pure functions with the source of
 * randomness injected, so the schedule can be tested without a clock or a socket.
 */

/** The delay before the first retry, before jitter. */
export const RECONNECT_BASE_DELAY_MS = 1_000

/** No retry ever waits longer than this, jitter included. */
export const RECONNECT_MAX_DELAY_MS = 60_000

/** The interval of the fallback unread-count poll while the hub is disconnected, before jitter. */
export const FALLBACK_POLL_INTERVAL_MS = 60_000

/** The fallback poll varies by up to this fraction of its interval either way. */
export const FALLBACK_POLL_JITTER_RATIO = 0.1

/** Clamps an injected random source to [0, 1), so a bad one can never push a delay out of bounds. */
const unit = (random: () => number): number => {
  const value = random()
  if (!Number.isFinite(value) || value < 0) return 0
  return value >= 1 ? 0.999_999 : value
}

/**
 * The delay before connection attempt number `retryCount + 1`, where `retryCount` is how many attempts in
 * a row have already failed (SignalR's `RetryContext.previousRetryCount`).
 *
 * Exponential back-off with "equal jitter": the ceiling doubles from {@link RECONNECT_BASE_DELAY_MS} on
 * each failure, is capped at {@link RECONNECT_MAX_DELAY_MS}, and the delay is drawn uniformly from the
 * upper half of it. The lower half is kept so a retry never fires hot; the random half spreads the
 * clients an API restart disconnected all at once, so they do not all come back in the same instant.
 * It never answers `null`: the connection retries for as long as the page wants it.
 */
export function reconnectDelayMs(retryCount: number, random: () => number = Math.random): number {
  const attempt = Number.isFinite(retryCount) && retryCount > 0 ? Math.floor(retryCount) : 0
  // 2 ** 1024 is Infinity, which Math.min turns back into the cap, so a very long outage is safe.
  const ceiling = Math.min(RECONNECT_MAX_DELAY_MS, RECONNECT_BASE_DELAY_MS * 2 ** attempt)
  const half = ceiling / 2
  return Math.round(half + unit(random) * half)
}

/**
 * The delay before the next fallback poll of the unread count, made while the hub is disconnected:
 * {@link FALLBACK_POLL_INTERVAL_MS}, varied by up to {@link FALLBACK_POLL_JITTER_RATIO} either way so the
 * tabs an outage disconnected together do not poll the API in lockstep.
 */
export function fallbackPollDelayMs(random: () => number = Math.random): number {
  const spread = FALLBACK_POLL_INTERVAL_MS * FALLBACK_POLL_JITTER_RATIO
  return Math.round(FALLBACK_POLL_INTERVAL_MS - spread + unit(random) * 2 * spread)
}
