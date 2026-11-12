import { getTranslation } from '@/i18n'
import { errorAlert } from './notification'
import { ApiError, getApiErrorMessages } from './api-error-helpers'

export function apiErrorAlert(error: ApiError, ignoreStatuses?: number[]) {
  const { t } = getTranslation()
  const parsed = getApiErrorMessages(error, t, ignoreStatuses ?? [])
  if (!parsed) return

  const html = parsed.messages.length > 0
    ? `<ul style="text-align:center;margin:0;padding-inline-start:1.2rem">${parsed.messages.map((m) => `<li>${m}</li>`).join('')}</ul>`
    : undefined

  errorAlert({ title: parsed.title, html })
}
