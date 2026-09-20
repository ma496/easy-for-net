import { type Middleware, isRejectedWithValue } from '@reduxjs/toolkit'
import { showServiceUnavailable } from '@/store/slices'

/**
 * Redux middleware that intercepts rejected RTK Query actions and turns the one
 * cross-cutting failure into state the app reacts to once, rather than in every
 * component: a request that never reached the API marks the backend unavailable.
 *
 * A tenant that stops being usable is not handled here. The API no longer refuses
 * a request on those grounds - what a session may do is settled when its token is
 * minted, and a tenant that has been suspended, deleted or left simply drops out
 * of the session at its next renewal. The renewal re-reads the account info, so
 * the route guard sees the tenant go and offers the caller another one; there is
 * no refusal to intercept.
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
