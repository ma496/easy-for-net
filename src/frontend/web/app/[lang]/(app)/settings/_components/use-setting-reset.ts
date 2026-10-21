'use client'

import { useTranslation } from '@/i18n'
import { apiErrorAlert, confirmDeleteAlert, successToast } from '@/lib/utils'
import { SettingName, SettingSource, useSettingDeleteMutation } from '@/store/api/settings'

/**
 * The Reset to inherited action of one setting card: confirms in the wording of the scope being
 * edited, removes that scope's overrides, and hands over to `onReset` - which refetches and remounts
 * the card - once the API has accepted it. A refusal is shown in an alert and leaves the card as it is.
 */
export const useSettingReset = (name: SettingName, settingTitle: string, ownSource: SettingSource, onReset: () => Promise<void>) => {
  const { t } = useTranslation()
  const [deleteSetting, { isLoading: isResetting }] = useSettingDeleteMutation()

  const reset = async () => {
    const confirmed = await confirmDeleteAlert({
      title: t('page.settings.resetTitle', { setting: settingTitle }) || `Reset ${settingTitle} settings?`,
      text:
        ownSource === SettingSource.Tenant
          ? t('page.settings.resetConfirmTenant', { setting: settingTitle }) || `This tenant's own ${settingTitle} settings will be removed, and the platform's values will apply instead.`
          : t('page.settings.resetConfirmPlatform', { setting: settingTitle }) || `The platform's own ${settingTitle} settings will be removed, and the defaults will apply instead. Tenants that set their own values keep them.`,
      confirmButtonText: t('page.settings.resetConfirmButton') || 'Yes, reset',
    })
    if (!confirmed.isConfirmed) return

    const result = await deleteSetting({ name })
    if (result.error) {
      apiErrorAlert(result.error)
      return
    }

    successToast.fire({ text: t('page.settings.resetSuccess', { setting: settingTitle }) || `${settingTitle} settings reset to inherited values` })
    await onReset()
  }

  return { reset, isResetting }
}
