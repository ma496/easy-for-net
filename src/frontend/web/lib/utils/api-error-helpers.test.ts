import { describe, expect, it } from 'vitest'
import { getApiErrorMessages, type ApiError } from './api-error-helpers'

/**
 * The tenant error codes the API can report, mirroring `ErrorHandling/ErrorCodes.cs`. Every one of
 * them has a message of its own in every locale, so a refusal arriving with one must never be shown
 * as the code itself.
 */
const tenantErrorCodes = [
  'tenantNotFound',
  'tenantSuspended',
  'notTenantMember',
  'noActiveTenant',
  'tenantIdentifierAlreadyExists',
  'duplicateTenantMembership',
  'lastTenantAdministrator',
  'systemCreatedTenantCannotBeModified',
  'crossTenantFileAccess',
  'tenantMembershipRevoked',
  'platformPermissionNotGrantable',
  'concurrentModification',
  'userSharedAcrossTenants',
] as const

/** The dictionary keys the fake below knows: the tenant codes, and the status titles and messages. */
const knownKeys = new Set([
  ...tenantErrorCodes.map((code) => `error.server.${code}`),
  'error.400.title',
  ...['401', '403', '404', '413', '415', '500'].flatMap((status) => [
    `error.${status}.title`,
    `error.${status}.message`,
  ]),
  'common.error',
])

/**
 * A dictionary standing in for the translation function, answering `translated:<key>` for the keys
 * it knows and the key itself for the ones it does not - which is what a real `t` does for a missing
 * key, and what makes "the key was shown to the user" a thing a test can catch.
 */
const t = (key: string): string => (knownKeys.has(key) ? `translated:${key}` : key)

/** A refusal body in the shape the API sends: the reason in prose, and the code alongside it. */
const refusalBody = (code: string, reason = 'The request was refused'): Record<string, unknown> => ({
  status: 403,
  title: 'One or more errors occurred!',
  detail: reason,
  errors: [{ name: '', reason, code }],
})

/** The error Redux Toolkit hands the screen for a refusal the API answered with that body. */
const refusal = (code: string, status = 403, reason?: string): ApiError => ({
  status,
  data: refusalBody(code, reason),
})

describe('getApiErrorMessages', () => {
  it('translates a tenant error code instead of showing it raw', () => {
    const result = getApiErrorMessages(refusal('tenantSuspended', 403, 'The tenant this session is working in is suspended.'), t)

    expect(result).not.toBeNull()
    expect(result!.messages).toEqual(['translated:error.server.tenantSuspended'])

    for (const message of result!.messages) {
      expect(message).not.toBe('tenantSuspended')
      expect(message).not.toBe('error.server.tenantSuspended')
    }
  })

  it.each(tenantErrorCodes)('translates a %s refusal rather than the code that names it', (code) => {
    const result = getApiErrorMessages(refusal(code), t)

    expect(result!.messages).toEqual([`translated:error.server.${code}`])
  })

  it('explains a suspended tenant and a revoked membership as the distinct reasons they are', () => {
    const suspended = getApiErrorMessages(refusal('tenantSuspended'), t)
    const revoked = getApiErrorMessages(refusal('tenantMembershipRevoked'), t)

    expect(suspended!.messages).toEqual(['translated:error.server.tenantSuspended'])
    expect(revoked!.messages).toEqual(['translated:error.server.tenantMembershipRevoked'])
    expect(suspended!.messages).not.toEqual(revoked!.messages)
  })

  it('falls back to the message for the status when the body carries no code', () => {
    const result = getApiErrorMessages({ status: 403, data: { status: 403, title: 'Forbidden' } }, t)

    expect(result!.messages).toEqual(['translated:error.403.message'])
  })

  it('falls back to the message for the status when the code has no message of its own', () => {
    const result = getApiErrorMessages(refusal('somethingTheDictionaryHasNeverHeardOf'), t)

    expect(result!.messages).toEqual(['translated:error.403.message'])
    expect(result!.messages.join(' ')).not.toContain('error.server.somethingTheDictionaryHasNeverHeardOf')
  })

  it('reads a code a body reports outside its errors as well', () => {
    const result = getApiErrorMessages({ status: 404, data: { status: 404, errorCode: 'tenantNotFound' } }, t)

    expect(result!.messages).toEqual(['translated:error.server.tenantNotFound'])
  })

  it('resolves a field error through the same keys, and keeps the reason when the dictionary has none', () => {
    const result = getApiErrorMessages(
      {
        status: 400,
        data: {
          status: 400,
          title: 'Validation Error',
          errors: [
            { name: 'Roles', reason: 'Roles must not be empty.', code: 'NotEmptyValidator' },
            { name: 'TenantId', reason: 'The tenant no longer exists.', code: 'tenantNotFound' },
          ],
        },
      },
      t,
    )

    expect(result!.messages).toEqual([
      'Roles must not be empty.',
      'translated:error.server.tenantNotFound',
    ])
  })
})