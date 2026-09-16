import { describe, expect, it } from 'vitest'
import { setTenantError, showServiceUnavailable } from '@/store/slices'
import { rtkErrorMiddleware } from './rtk-error-middleware'

/**
 * Tests what the one middleware that reacts to a refused request does with the answer, and - just as
 * importantly - what it leaves alone (AC-070, AC-127).
 *
 * A request refused because the tenant the session was acting in is gone has to become state the app
 * reacts to once, so the user is told why and offered another tenant. Every other failure has to stay
 * the calling screen's business: a `400` is an input failure the screen reports in place, a permission
 * the caller lacks is not a reason to move them, and none of them is a reason to end the session.
 */

/** A rejected RTK Query action carrying the given payload, shaped the way the middleware reads it. */
const rejectedQuery = (payload: unknown) => ({
  type: 'appApi/executeQuery/rejected',
  payload,
  error: { message: 'Rejected' },
  meta: { requestId: 'request-1', requestStatus: 'rejected', rejectedWithValue: true, arg: {} },
})

/** The problem-details body the API answers a tenant-scoped refusal with. */
const refusal = (code: string, status: number) => ({
  status,
  data: {
    status,
    title: 'One or more errors occurred!',
    errors: [{ name: '', code, reason: 'The tenant is no longer usable' }],
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

/** The tenant error codes the middleware records, one per way a session's tenant can stop being usable. */
const tenantRefusalCodes = [
  'tenantSuspended',
  'tenantMembershipRevoked',
  'tenantNotFound',
  'noActiveTenant',
  'notTenantMember',
]

describe('rtkErrorMiddleware', () => {
  it.each(tenantRefusalCodes)('records %s so the user can be told why and offered another tenant', (code) => {
    const dispatched = through(rejectedQuery(refusal(code, 403)))

    expect(dispatched).toEqual([setTenantError(code)])
  })

  it.each(tenantRefusalCodes)('ignores %s when it comes back as an input failure rather than a refusal', (code) => {
    const dispatched = through(rejectedQuery(refusal(code, 400)))

    expect(dispatched).toEqual([])
  })

  it('records nothing for a permission the caller merely lacks, which is not a reason to move them', () => {
    const dispatched = through(rejectedQuery(refusal('permissionDenied', 403)))

    expect(dispatched).toEqual([])
  })

  it('records nothing for a refusal it does not recognise, so a new code cannot be guessed at', () => {
    const dispatched = through(rejectedQuery(refusal('somethingAddedLater', 403)))

    expect(dispatched).toEqual([])
  })

  it('marks the service unavailable when the request never reached the API', () => {
    const dispatched = through(rejectedQuery({ status: 'FETCH_ERROR', error: 'Failed to fetch' }))

    expect(dispatched).toEqual([showServiceUnavailable()])
  })

  it('never dispatches a sign-out, whatever was refused', () => {
    const dispatched = [
      ...through(rejectedQuery(refusal('tenantSuspended', 403))),
      ...through(rejectedQuery({ status: 'FETCH_ERROR' })),
      ...through(rejectedQuery(refusal('tenantSuspended', 400))),
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
