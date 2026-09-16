import { type Middleware, isRejectedWithValue } from '@reduxjs/toolkit'
import { setTenantError, showServiceUnavailable } from '@/store/slices'

/**
 * The refusal codes the API answers a tenant-scoped request with when the tenant
 * the session is acting in can no longer be acted in: it was suspended or
 * deleted, the caller's membership in it ended, or no tenant is established at
 * all. They are recorded, never acted on by signing the user out - the session
 * stays valid and the user is offered another tenant instead.
 */
const tenantRefusalCodes: readonly string[] = [
  'tenantSuspended',
  'tenantMembershipRevoked',
  'tenantNotFound',
  'noActiveTenant',
  'notTenantMember',
]

/**
 * The status the tenant-context pre-processor refuses with. The same codes also
 * come back as `400` from individual endpoints - adding a member to a suspended
 * tenant, uploading with no tenant, addressing a tenant that does not exist -
 * and those are input failures the calling screen reports in place through
 * `apiErrorAlert`. Only a `403` means the session's own tenant went away, so
 * only a `403` is turned into a tenant error the app reacts to.
 */
const tenantRefusalStatus = 403

/**
 * Returns the tenant refusal code carried by a rejected query payload, or
 * `undefined` when the failure is not a tenant refusal. The body is the standard
 * problem-details shape the API emits for every failure, so the code sits in
 * `data.errors[].code`.
 */
const getTenantRefusalCode = (payload: unknown): string | undefined => {
  if (typeof payload !== 'object' || payload === null) return undefined

  const { status, data } = payload as { status?: unknown; data?: unknown }
  if (status !== tenantRefusalStatus) return undefined
  if (typeof data !== 'object' || data === null) return undefined

  const { errors } = data as { errors?: unknown }
  if (!Array.isArray(errors)) return undefined

  for (const error of errors) {
    const code = (error as { code?: unknown } | null)?.code
    if (typeof code === 'string' && tenantRefusalCodes.includes(code)) return code
  }
  return undefined
}

/**
 * Redux middleware that intercepts rejected RTK Query actions and turns the two
 * cross-cutting failures into state the app reacts to once, rather than in every
 * component: a request that never reached the API marks the backend unavailable,
 * and a tenant-scoped request the API refused because the session's tenant is
 * gone records the reason on the auth slice. The second case deliberately keeps
 * the user authenticated - losing a tenant is a reason to offer them another
 * one, not to end their session or their access to the tenants they still hold.
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

    const tenantRefusalCode = getTenantRefusalCode(payload)
    if (tenantRefusalCode) {
      api.dispatch(setTenantError(tenantRefusalCode))
    }
  }
  return next(action)
}
