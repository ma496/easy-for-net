import { type Middleware, isRejectedWithValue } from '@reduxjs/toolkit'
import { showServiceUnavailable } from '@/store/slices'

/**
 * Redux middleware that intercepts rejected RTK Query actions and turns the one
 * cross-cutting failure into state the app reacts to once, rather than in every
 * component: a request that never reached the API marks the backend unavailable.
 */
export const rtkErrorMiddleware: Middleware = (api) => (next) => (action: unknown) => {
  if (isRejectedWithValue(action)) {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const rejectedAction = action as any
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const payload: any = rejectedAction.payload ?? rejectedAction.error
    if (payload?.status === 'FETCH_ERROR') {
      api.dispatch(showServiceUnavailable())
    }
  }
  return next(action)
}
