import type { FetchBaseQueryError } from '@reduxjs/toolkit/query'
import type { SerializedError } from '@reduxjs/toolkit'

export type ApiError = FetchBaseQueryError | SerializedError | { messages: string[] } | undefined | null

/**
 * A single field or request-level error the way the API reports it. `name` is the camelCased
 * request property the refusal belongs to (empty for a request-level refusal, the feature name for a
 * plan refusal), `code` is the `ErrorCodes` constant or a FluentValidation rule name, and `reason` is
 * the message to show, exactly as the API sent it. A code this application defines is localized into
 * the caller's `Accept-Language` culture from `error.server.{code}` there, `${propertyName}` and all —
 * a bare FluentValidation rule with no `error.server.*` entry of its own instead carries
 * FluentValidation's own built-in per-culture message, already translated into that same culture by
 * FluentValidation itself rather than by this application's resource dictionary, with the raw
 * property name standing in the sentence where `${propertyName}` would put the translated field
 * label. Neither case is this application's to translate again.
 */
export interface ValidationError {
  name: string
  code: string | number
  reason: string
}

/** Type guard that narrows an unknown value to FetchBaseQueryError. */
function isFetchBaseQueryError(error: unknown): error is FetchBaseQueryError {
  return typeof error === 'object' && error !== null && 'status' in error
}

/** Type guard that narrows an unknown value to an object with a string `message` property. */
function isErrorWithMessage(error: unknown): error is { message: string } {
  return (
    typeof error === 'object' &&
    error !== null &&
    'message' in error &&
    typeof (error as { message: unknown }).message === 'string'
  )
}

/**
 * The `errors` a coded API response carries - every field and request-level refusal, in the shape
 * `ExceptionProcessor` and `AuthorizationRefusalResultHandler` both answer with, whatever the status -
 * or `null` when the body carries none.
 */
function getValidationErrors(data: unknown): ValidationError[] | null {
  if (!data || typeof data !== 'object') return null

  const body = data as Record<string, unknown>

  return Array.isArray(body.errors) && body.errors.length > 0 ? (body.errors as ValidationError[]) : null
}

/**
 * The machine-readable code a failed response's first error carries, or `null` when it carries none.
 *
 * `getApiErrorMessages` itself has no use for this — it shows the `reason` the API already localized
 * — but a screen that needs to *branch* on what specifically refused a request (a feature gate, a
 * tenant refusal) rather than just display it still needs the code itself.
 */
export function getErrorCode(error: ApiError): string | null {
  if (!isFetchBaseQueryError(error) || typeof error.status !== 'number') return null

  const errors = getValidationErrors(error.data)
  const code = errors?.[0]?.code

  return typeof code === 'string' && code.length > 0 ? code : null
}

/**
 * Extracts the error message string from a FetchBaseQueryError or SerializedError, for a body that
 * carries no structured `errors` to read a reason from - a transport-level failure, or a response
 * this application did not shape itself.
 */
function getErrorMessage(error: unknown): string | null {
  if (isFetchBaseQueryError(error)) {
    if (error.status === 'FETCH_ERROR') {
      return error.error
    }
    if (error.status === 'PARSING_ERROR') {
      return error.error
    }
    if (error.status === 'CUSTOM_ERROR') {
      return error.error
    }
    // Numeric status — data may still name its reason at the top level
    const data = error.data
    if (data && typeof data === 'object') {
      const obj = data as Record<string, unknown>
      if (typeof obj.detail === 'string' && obj.detail.length > 0) return obj.detail
      if (typeof obj.message === 'string') return obj.message
      if (typeof obj.title === 'string') return obj.title
    }
    return null
  }
  if (isErrorWithMessage(error)) {
    return error.message
  }
  if (error && typeof error === 'object') {
    const obj = error as Record<string, unknown>
    if (typeof obj.message === 'string') return obj.message
    if (typeof obj.error === 'string') return obj.error
  }
  return null
}

/**
 * The generic title for an HTTP status this application recognizes, or `common.error` for one it
 * does not. Independent of whatever the body says refused the request — the status is what this
 * application can always name, whatever the response looked like.
 */
function titleForStatus(status: number, t: (key: string) => string): string {
  const key = `error.${status}.title`
  const translated = t(key)
  return translated !== key ? translated : t('common.error')
}

/**
 * The generic message for an HTTP status this application recognizes, for a response that named no
 * reason of its own to show instead - a body with no `errors`, `detail`, `message` or `title`.
 */
function messageForStatus(status: number, t: (key: string) => string): string {
  const key = `error.${status}.message`
  const translated = t(key)
  return translated !== key ? translated : t('error.500.message')
}

/**
 * Extracts a human-readable title and message list from any RTK error shape.
 *
 * A response that carries `errors` — every refusal this application raises itself, whatever the
 * status — is shown exactly as the API sent it: one message per error, read from `reason` with no
 * translation of its own, since the API already localized it into the caller's UI language. Only the
 * title, and the message for a response that names no reason at all, are this application's own copy,
 * because a bare HTTP status is the one thing here that is never itself localized.
 */
export function getApiErrorMessages(
  error: ApiError,
  t: (key: string, vars?: Record<string, string | number>) => string,
  ignoreStatuses: number[] = [],
): { title: string; messages: string[] } | null {
  if (!error) return null

  // Determine the status and data from the error shape
  let status: number | string | undefined
  let data: unknown | undefined

  if (isFetchBaseQueryError(error)) {
    status = error.status
    data = typeof error.status === 'number' ? error.data : undefined
  }

  // Numeric HTTP status
  if (typeof status === 'number' && !ignoreStatuses.includes(status)) {
    const errors = getValidationErrors(data)
    if (errors) {
      return { title: titleForStatus(status, t), messages: errors.map((e) => e.reason) }
    }

    const msg = getErrorMessage(error)
    return { title: titleForStatus(status, t), messages: [msg ?? messageForStatus(status, t)] }
  }

  // Plain { messages: string[] } object
  if (error && typeof error === 'object' && 'messages' in error && Array.isArray((error as Record<string, unknown>).messages)) {
    return {
      title: t('common.error'),
      messages: (error as { messages: string[] }).messages,
    }
  }

  // A transport-level failure (FETCH_ERROR / PARSING_ERROR / CUSTOM_ERROR) or any other unknown shape
  const msg = getErrorMessage(error)
  return {
    title: t('common.error'),
    messages: [msg ?? t('error.500.message')],
  }
}
