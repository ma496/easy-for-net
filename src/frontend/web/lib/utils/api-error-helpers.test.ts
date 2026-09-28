import { describe, expect, it } from 'vitest'
import { getApiErrorMessages, getErrorCode, type ApiError } from './api-error-helpers'

/** The dictionary keys the fake `t` below knows: the generic status chrome this application still owns. */
const knownKeys = new Set([
  'error.400.title',
  'error.403.title',
  'error.403.message',
  'common.error',
])

/**
 * A dictionary standing in for the translation function, answering `translated:<key>` for the keys it
 * knows and the key itself for the ones it does not - the same as a real `t` does for a missing key,
 * which is what makes "a code was translated on the client" a thing a test can catch.
 */
const t = (key: string): string => (knownKeys.has(key) ? `translated:${key}` : key)

/** A refusal body in the shape the API sends: one or more errors, each already carrying the message to show. */
const refusalBody = (errors: { name?: string; code: string; reason: string }[], status = 403): Record<string, unknown> => ({
  status,
  title: 'One or more errors occurred!',
  errors: errors.map((e) => ({ name: e.name ?? '', ...e })),
})

/** The error Redux Toolkit hands the screen for a refusal the API answered with that body. */
const refusal = (errors: { name?: string; code: string; reason: string }[], status = 403): ApiError => ({
  status,
  data: refusalBody(errors, status),
})

describe('getApiErrorMessages', () => {
  it('shows the reason the API already localized, unchanged, for a request-level refusal', () => {
    const result = getApiErrorMessages(refusal([{ code: 'tenantSuspended', reason: 'Ce compte a été suspendu.' }]), t)

    expect(result!.messages).toEqual(['Ce compte a été suspendu.'])
  })

  it('never looks the code up as a translation key of its own', () => {
    const result = getApiErrorMessages(refusal([{ code: 'tenantSuspended', reason: 'The tenant this session is working in is suspended.' }]), t)

    for (const message of result!.messages) {
      expect(message).not.toBe('tenantSuspended')
      expect(message).not.toBe('error.server.tenantSuspended')
    }
  })

  it('shows one message per field error, in the order the API sent them, whatever code each carries', () => {
    const result = getApiErrorMessages(
      refusal(
        [
          { name: 'roles', code: 'NotEmptyValidator', reason: 'Roles must not be empty.' },
          { name: 'tenantId', code: 'tenantNotFound', reason: 'The tenant no longer exists.' },
        ],
        400,
      ),
      t,
    )

    expect(result!.messages).toEqual(['Roles must not be empty.', 'The tenant no longer exists.'])
  })

  it('shows the reason a hand-shaped refusal carries whatever its status', () => {
    const result = getApiErrorMessages(refusal([{ name: 'FileManagement', code: 'featureDisabled', reason: "This plan doesn't include file uploads." }]), t)

    expect(result!.title).toBe('translated:error.403.title')
    expect(result!.messages).toEqual(["This plan doesn't include file uploads."])
  })

  it('titles the alert from the status, not from the refusal', () => {
    const result = getApiErrorMessages(refusal([{ code: 'tenantRequired', reason: 'A tenant is required.' }], 400), t)

    expect(result!.title).toBe('translated:error.400.title')
  })

  it('falls back to the status message when the body carries no errors, detail, message or title', () => {
    const result = getApiErrorMessages({ status: 403, data: { status: 403 } }, t)

    expect(result!.messages).toEqual(['translated:error.403.message'])
  })

  it('falls back to the generic status message when this status has none of its own', () => {
    const result = getApiErrorMessages({ status: 599, data: {} }, t)

    expect(result!.title).toBe('translated:common.error')
    expect(result!.messages).toEqual(['error.500.message'])
  })

  it('shows a top-level detail when the body carries no structured errors', () => {
    const result = getApiErrorMessages({ status: 500, data: { status: 500, detail: 'The database is unreachable.' } }, t)

    expect(result!.messages).toEqual(['The database is unreachable.'])
  })

  it('ignores a status the caller asked to ignore', () => {
    const result = getApiErrorMessages(refusal([{ code: 'tenantNotFound', reason: 'Not found.' }], 404), t, [404])

    expect(result!.title).toBe('translated:common.error')
  })

  it('reports a transport-level failure without touching the status machinery', () => {
    const result = getApiErrorMessages({ status: 'FETCH_ERROR', error: 'Failed to fetch' } as ApiError, t)

    expect(result).toEqual({ title: 'translated:common.error', messages: ['Failed to fetch'] })
  })

  it('reads a plain { messages } object as-is', () => {
    const result = getApiErrorMessages({ messages: ['Something went wrong.'] }, t)

    expect(result).toEqual({ title: 'translated:common.error', messages: ['Something went wrong.'] })
  })

  it('returns null for no error at all', () => {
    expect(getApiErrorMessages(undefined, t)).toBeNull()
    expect(getApiErrorMessages(null, t)).toBeNull()
  })
})

describe('getErrorCode', () => {
  it('reads the code the first error carries', () => {
    expect(getErrorCode(refusal([{ code: 'tenantRequired', reason: 'A tenant is required.' }], 400))).toBe('tenantRequired')
  })

  it('reads only the first code when a request carries several errors', () => {
    expect(
      getErrorCode(
        refusal(
          [
            { name: 'roles', code: 'NotEmptyValidator', reason: 'Roles must not be empty.' },
            { name: 'tenantId', code: 'tenantNotFound', reason: 'The tenant no longer exists.' },
          ],
          400,
        ),
      ),
    ).toBe('NotEmptyValidator')
  })

  it('reports no code for a transport-level failure', () => {
    expect(getErrorCode({ status: 'FETCH_ERROR', error: 'Failed to fetch' } as ApiError)).toBeNull()
  })

  it('reports no code for a body with no errors', () => {
    expect(getErrorCode({ status: 500, data: { status: 500 } })).toBeNull()
  })
})
