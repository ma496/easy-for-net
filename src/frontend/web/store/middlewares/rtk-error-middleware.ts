import { type Middleware, isRejectedWithValue } from '@reduxjs/toolkit'
import { rtkErrorHandler } from '@/lib/utils'

const ignoreEndpoints = ['getUserInfo']

/**
 * Redux middleware that intercepts rejected RTK Query actions and routes
 * their payloads through the centralized rtkErrorHandler, while skipping
 * a configured list of endpoints (e.g. getUserInfo) to avoid noisy errors.
 */
export const rtkErrorMiddleware: Middleware = (_api) => (next) => (action: unknown) => {
  if (isRejectedWithValue(action)) {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const rejectedAction = action as any
    if (ignoreEndpoints.includes(rejectedAction.meta?.arg?.endpointName)) return next(action)
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const payload: any = rejectedAction.payload ?? rejectedAction.error
    rtkErrorHandler(payload)
  }
  return next(action)
}
