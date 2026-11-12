import { describe, expect, it } from 'vitest'
import { showServiceUnavailable } from '@/store/slices'
import { rtkErrorMiddleware } from './rtk-error-middleware'

/**
 * Tests what the one middleware that reacts to a refused request does with the answer, and - just as
 * importantly - what it leaves alone.
 *
 * A request that never reached the API is the only failure that becomes state the app reacts to
 * once. Every other failure stays the calling screen's business: a `400` is an input failure the
 * screen reports in place, a permission the caller lacks is not a reason to move them, and none of
 * them is a reason to end the session.
 */

/** A rejected RTK Query action carrying the given payload, shaped the way the middleware reads it. */
const rejectedQuery = (payload: unknown) => ({
  type: 'appApi/executeQuery/rejected',
  payload,
  error: { message: 'Rejected' },
  meta: { requestId: 'request-1', requestStatus: 'rejected', rejectedWithValue: true, arg: {} },
})

/** The problem-details body the API answers a refusal with. */
const refusal = (code: string, status: number) => ({
  status,
  data: {
    status,
    title: 'One or more errors occurred!',
    errors: [{ name: '', code, reason: 'The request was refused' }],
  },
})

/**
 * Runs one action through the middleware and reports, in order, everything it dispatched - passing
 * the action on to the next middleware being the one thing it has to do whatever the action was.
 */
const through = (action: unknown): unknown[] => {
  const dispatched: unknown[] = []
  const api = {
    dispatch: (inner: unknown) => {
      dispatched.push(inner)
      return inner
    },
    getState: () => undefined,
  }
  const next = (inner: unknown) => inner

  rtkErrorMiddleware(api as never)(next as never)(action)

  return dispatched
}

describe('rtkErrorMiddleware', () => {
  it('marks the service unavailable when the request never reached the API', () => {
    const dispatched = through(rejectedQuery({ status: 'FETCH_ERROR', error: 'Failed to fetch' }))

    expect(dispatched).toEqual([showServiceUnavailable()])
  })

  it.each([
    ['permissionDenied', 403],
    ['userNotActive', 400],
    ['roleNotFound', 400],
    ['somethingAddedLater', 403],
  ] as const)('leaves a %s refusal to the screen that made the request', (code, status) => {
    const dispatched = through(rejectedQuery(refusal(code, status)))

    expect(dispatched).toEqual([])
  })

  it('never dispatches a sign-out, whatever was refused', () => {
    const dispatched = [
      ...through(rejectedQuery(refusal('permissionDenied', 403))),
      ...through(rejectedQuery({ status: 'FETCH_ERROR' })),
      ...through(rejectedQuery(refusal('userNotActive', 400))),
    ]

    expect(dispatched.map((action) => (action as { type: string }).type)).not.toContain('auth/signout')
  })

  it('leaves a fulfilled or a pending action entirely alone', () => {
    const fulfilled = { type: 'appApi/executeQuery/fulfilled', payload: {}, meta: { requestId: 'r' } }
    const unrelated = { type: 'auth/signout' }

    expect(through(fulfilled)).toEqual([])
    expect(through(unrelated)).toEqual([])
  })
})
