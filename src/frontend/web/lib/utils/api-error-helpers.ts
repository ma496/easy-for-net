import type { FetchBaseQueryError } from '@reduxjs/toolkit/query'
import type { SerializedError } from '@reduxjs/toolkit'

export type ApiError = FetchBaseQueryError | SerializedError | { messages: string[] } | undefined | null

/**
 * Represents a single validation error returned by the backend API.
 * `name` is the property name (e.g. "EmailNormalized"),
 * `code` is the error code (e.g. "duplicate_email"),
 * `reason` is the fallback message.
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
 * Extracts the error message string from a FetchBaseQueryError or SerializedError.
 * Returns `null` if the error shape is unknown.
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
    // Numeric status — data may have a message
    const data = error.data
    if (data && typeof data === 'object') {
      const obj = data as Record<string, unknown>
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
 * Extracts the machine-readable error code a failed response carries, or `null` when it
 * carries none.
 *
 * A response the application raised itself names its reason twice: once in prose, and once
 * as a code from the API's catalogue. The code is what the web side can translate, and
 * FastEndpoints reports it inside `errors`, alongside the property it belongs to — an empty
 * name for a refusal that belongs to the request as a whole (`tenantSuspended`), the
 * property name for one that belongs to a field, and the FluentValidation rule name
 * (`NotEmptyValidator`) for a rule that failed. A body that names its reason at the top
 * level instead is read too, since a hand-shaped response has no `errors` to read.
 */
function getErrorCode(data: unknown): string | null {
  if (!data || typeof data !== 'object') return null

  const body = data as Record<string, unknown>

  if (Array.isArray(body.errors)) {
    for (const error of body.errors) {
      if (error && typeof error === 'object') {
        const code = (error as Record<string, unknown>).code
        if (typeof code === 'string' && code.length > 0) return code
      }
    }
  }

  if (typeof body.errorCode === 'string' && body.errorCode.length > 0) return body.errorCode

  return null
}

/**
 * The translated message for the error code a response carries, or `null` when it carries no
 * code or the code has no message of its own.
 *
 * The dictionary lookup is the same one validation errors go through, so a code means the
 * same thing wherever it arrives. A key that is missing comes back as the key itself, which
 * is how a code with a message is told from one without — a code the dictionary has never
 * heard of must not be shown to the user as `error.server.somethingNew`.
 */
function getTranslatedErrorCode(
  data: unknown,
  t: (key: string, vars?: Record<string, string | number>) => string,
): string | null {
  const code = getErrorCode(data)

  if (!code) return null

  const key = `error.server.${code}`
  const translated = t(key)

  return translated !== key ? translated : null
}

/**
 * Maps a backend `ValidationError.name` to the corresponding Formik field name.
 *
 * The backend returns `PascalCase` names, optionally with a `Normalized` suffix
 * (e.g. `"EmailNormalized"`, `"UsernameNormalized"`, `"FirstName"`).
 * This function strips the `Normalized` suffix and lowercases the first character.
 *
 * Examples:
 *   "EmailNormalized" → "email"
 *   "UsernameNormalized" → "username"
 *   "PasswordNormalized" → "password"
 *   "FirstName" → "firstName"
 *   "LastName" → "lastName"
 *   "Roles" → "roles"
 */
function toFormFieldName(name: string): string {
  const stripped = name.replace(/normalized$/i, '')
  return stripped.charAt(0).toLowerCase() + stripped.slice(1)
}

/**
 * Formats a single `ValidationError` into a localized message string.
 *
 * Attempts to look up `error.server.{code}` in the translation dictionary.
 * Falls back to `error.reason` if no translation key exists.
 * The property name is localized via `t(name)`.
 */
function getFieldErrorMessage(
  error: ValidationError,
  t: (key: string, vars?: Record<string, string | number>) => string,
): string {
  const fieldName = t(toFormFieldName(error.name))
  const key = `error.server.${error.code}`
  const translated = t(key, { propertyName: fieldName })
  // If the translation returned the key itself, it doesn't exist — fall back
  return translated !== key ? translated : error.reason
}

/**
 * Extracts a human-readable title and message list from any RTK error shape.
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
    // 400 with validation errors
    if (status === 400 && data && typeof data === 'object') {
      const obj = data as Record<string, unknown>
      const validationErrors = obj.errors
      if (Array.isArray(validationErrors) && validationErrors.length > 0) {
        const msgs = validationErrors.map((e: ValidationError) => getFieldErrorMessage(e, t))
        return { title: t('error.400.title'), messages: msgs }
      }
      // 400 without structured errors — a coded refusal is translated, anything else is
      // shown as the data reports it
      const coded = getTranslatedErrorCode(data, t)
      if (coded) {
        return { title: t('error.400.title'), messages: [coded] }
      }
      const msg = getErrorMessage(error)
      return {
        title: t('error.400.title'),
        messages: msg ? [msg] : [JSON.stringify(data)],
      }
    }
    // Every other status is answered with the message for that status unless the body names
    // a reason the dictionary knows. A bare status says what went wrong with the request; a
    // code says why, which is the part a user can act on — "this tenant is suspended" rather
    // than "Forbidden".
    if (status === 401) {
      return { title: t('error.401.title'), messages: [getTranslatedErrorCode(data, t) ?? t('error.401.message')] }
    }
    if (status === 403) {
      return { title: t('error.403.title'), messages: [getTranslatedErrorCode(data, t) ?? t('error.403.message')] }
    }
    if (status === 404) {
      return { title: t('error.404.title'), messages: [getTranslatedErrorCode(data, t) ?? t('error.404.message')] }
    }
    if (status === 413) {
      return { title: t('error.413.title'), messages: [getTranslatedErrorCode(data, t) ?? t('error.413.message')] }
    }
    if (status === 415) {
      return { title: t('error.415.title'), messages: [getTranslatedErrorCode(data, t) ?? t('error.415.message')] }
    }
    if (status === 500) {
      return { title: t('error.500.title'), messages: [getTranslatedErrorCode(data, t) ?? t('error.500.message')] }
    }
    // Unknown status — a coded reason, else whatever message the data carries
    const unknownCoded = getTranslatedErrorCode(data, t)
    if (unknownCoded) {
      return { title: t('common.error'), messages: [unknownCoded] }
    }
    const msg = getErrorMessage(error)
    return {
      title: t('common.error'),
      messages: msg ? [msg] : [t('error.500.message')],
    }
  }

  // Plain { messages: string[] } object
  if ('messages' in error && Array.isArray((error as Record<string, unknown>).messages)) {
    return {
      title: t('common.error'),
      messages: (error as { messages: string[] }).messages,
    }
  }

  // SerializedError or other unknown shapes — consulted for a code the same way, so a reason
  // arriving without a status is translated exactly as one arriving with it
  const transientCoded = getTranslatedErrorCode(error, t)
  if (transientCoded) {
    return { title: t('common.error'), messages: [transientCoded] }
  }

  const msg = getErrorMessage(error)
  return {
    title: t('common.error'),
    messages: msg ? [msg] : [t('error.500.message')],
  }
}
