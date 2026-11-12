'use client'

import { useTranslation } from '@/i18n'
import { apiErrorAlert, confirmDeleteAlert, successToast } from '@/lib/utils'
import { SettingName, useSettingDeleteMutation } from '@/store/api/settings'

/**
 * The Reset to defaults action of one setting card: confirms, removes the site's overrides, and hands over to `onReset` - which refetches and remounts
 * the card - once the API has accepted it. A refusal is shown in an alert and leaves the card as it is.
 */
export const useSettingReset = (name: SettingName, settingTitle: string, onReset: () => Promise<void>) => {
  const { t } = useTranslation()
  const [deleteSetting, { isLoading: isResetting }] = useSettingDeleteMutation()

  const reset = async () => {
    const confirmed = await confirmDeleteAlert({
      title: t('page.settings.resetTitle', { setting: settingTitle }) || `Reset ${settingTitle} settings?`,
      text: t('page.settings.resetConfirm', { setting: settingTitle }) || `The site's own ${settingTitle} settings will be removed, and the defaults will apply instead.`,
      confirmButtonText: t('page.settings.resetConfirmButton') || 'Yes, reset',
    })
    if (!confirmed.isConfirmed) return

    const result = await deleteSetting({ name })
    if (result.error) {
      apiErrorAlert(result.error)
      return
    }

    successToast.fire({ text: t('page.settings.resetSuccess', { setting: settingTitle }) || `${settingTitle} settings reset to defaults` })
    await onReset()
  }

  return { reset, isResetting }
}
