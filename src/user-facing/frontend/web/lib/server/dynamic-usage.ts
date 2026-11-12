import { MissingServerApiUrlError } from '@/config'

/**
 * Whether an error must be let through rather than answered with a fallback: Next's signal that the
 * route reads the request (thrown by `headers()` while it tries to prerender, which is how a route is
 * marked dynamic), or a deployment that never said where the API is.
 */
export const mustRethrow = (error: unknown): boolean =>
  error instanceof MissingServerApiUrlError || (!!error && typeof error === 'object' && 'digest' in error && (error as { digest?: unknown }).digest === 'DYNAMIC_SERVER_USAGE')
