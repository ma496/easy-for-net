'use client'
import { useTranslation } from '@/i18n'
import { cn, ApiError, getApiErrorMessages } from '@/lib/utils'
import { AlertCircle, X } from 'lucide-react'
import { useState } from 'react'

/**
 * Props for the ApiErrorMessages component.
 *
 * Accepts a single RTK Query error type (`FetchBaseQueryError`, `SerializedError`)
 * or plain `{ messages: string[] }` object.
 * Renders nothing when the value is nullish.
 */
export interface ApiErrorMessagesProps {
  /** A single RTK Query error or plain error object to display. Nullish values are ignored. */
  error?: ApiError
  /** Optional CSS class name to add to the wrapper. */
  className?: string
  /** If true, the banner can be dismissed by the user (default: true). */
  dismissible?: boolean
  /** HTTP status codes to ignore — falls through to generic error handling. */
  ignoreStatuses?: number[]
}

/**
 * ApiErrorMessages renders a localized, dismissible danger alert banner for
 * RTK Query errors. It accepts a single error, de-duplicates identical messages,
 * and displays a combined title + message list.
 *
 * Usage:
 * ```tsx
 * const [createApi, { error: createError }] = useCreateMutation()
 * return (
 *   <Form>
 *     <ApiErrorMessages error={createError} />
 *     <FormInput name="email" ... />
 *   </Form>
 * )
 * ```
 */
export const ApiErrorMessages = ({
  error,
  className,
  dismissible = false,
  ignoreStatuses,
}: ApiErrorMessagesProps) => {
  const { t } = useTranslation()
  const [dismissedError, setDismissedError] = useState<ApiError | null>(null)
  const dismissed = error === dismissedError

  if (!error || dismissed) return null

  const parsed = getApiErrorMessages(error, t, ignoreStatuses ?? [])
  if (!parsed) return null

  const { title, messages } = parsed

  return (
    <div
      role="alert"
      className={cn(
        'relative flex items-start gap-3 rounded-lg border border-danger/30 bg-danger/10 p-4 text-sm',
        className,
      )}
    >
      <AlertCircle className="mt-0.5 h-5 w-5 shrink-0 text-danger" />
      <div className="flex-1 space-y-1">
        <p className="font-semibold text-danger">
          {title}
        </p>
        {messages.length > 0 && (
          <ul className="list-inside list-disc space-y-0.5 text-danger/80">
            {messages.map((msg, i) => (
              <li key={i}>{msg}</li>
            ))}
          </ul>
        )}
      </div>
      {dismissible && (
        <button
          type="button"
          onClick={() => setDismissedError(error)}
          className="cursor-pointer text-danger/60 hover:text-danger"
          aria-label={t('common.close') ?? 'Close'}
        >
          <X className="h-4 w-4" />
        </button>
      )}
    </div>
  )
}

ApiErrorMessages.displayName = 'ApiErrorMessages'
