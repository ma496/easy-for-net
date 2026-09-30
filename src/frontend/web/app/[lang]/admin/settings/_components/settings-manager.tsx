'use client'

import { useState } from 'react'
import { Settings2 } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { useAppSelector } from '@/store/hooks'
import { isAllowed } from '@/lib/utils'
import { ApiErrorMessages, Loader } from '@/components/ui'
import { Allow } from '@/allow'
import { findSetting, SettingName, toEmailSettings, toSigninSettings, useSettingListQuery } from '@/store/api/settings'
import { ownSourceOf } from './settings-form'
import { SigninSettingsCard } from './signin-settings-card'
import { EmailSettingsCard } from './email-settings-card'

/**
 * Client-side shell for `/admin/settings`: the scope notice (which tenant, or the platform, is being
 * edited, and whether the caller may change it) and one card per known setting in a fixed order -
 * Sign-in, then Email. A setting the API does not declare is not shown; one it declares that this
 * screen does not know is ignored. Each card is its own form and is remounted, keyed by a counter,
 * after its overrides are reset, so it re-seeds from the freshly fetched values.
 */
export const SettingsManager = () => {
  const { t } = useTranslation()
  const authState = useAppSelector((state) => state.auth)
  const canUpdate = isAllowed(authState, [Allow.Settings_Update])
  const activeTenant = authState.activeTenant
  const ownSource = ownSourceOf(!!activeTenant)

  const { data, isLoading, error, refetch } = useSettingListQuery()
  const [resetKeys, setResetKeys] = useState<{ [name in SettingName]?: number }>({})

  const remountAfterReset = (name: SettingName) => async () => {
    await refetch()
    setResetKeys((previous) => ({ ...previous, [name]: (previous[name] ?? 0) + 1 }))
  }

  const banner = (
    <div className="flex items-start gap-2 rounded-md border border-info/30 bg-info/10 p-3 text-sm text-info">
      <Settings2 className="mt-0.5 h-4 w-4 shrink-0" />
      <div className="min-w-0 wrap-break-word">
        <div>{activeTenant ? t('page.settings.scope.tenant', { tenant: activeTenant.name }) : t('page.settings.scope.platform')}</div>
        {!canUpdate && <div>{t('page.settings.readOnly')}</div>}
      </div>
    </div>
  )

  const content = () => {
    if (isLoading) {
      return (
        <div className="flex min-h-64 items-center justify-center">
          <Loader />
        </div>
      )
    }

    if (error || !data) {
      return (
        <div className="rounded-md border border-white-light p-4 dark:border-[#1b2e4b]">
          <ApiErrorMessages error={error} />
        </div>
      )
    }

    const signinDto = findSetting(data.items, SettingName.Signin)
    const signin = signinDto ? toSigninSettings(signinDto) : null
    const emailDto = findSetting(data.items, SettingName.Email)
    const email = emailDto ? toEmailSettings(emailDto) : null

    if (!signin && !email) {
      return <div className="flex items-center justify-center py-10 text-center text-gray-500 dark:text-gray-400">{t('page.settings.empty')}</div>
    }

    return (
      <>
        {signin && (
          <SigninSettingsCard
            key={`${SettingName.Signin}:${resetKeys.Signin ?? 0}`}
            settings={signin}
            ownSource={ownSource}
            canUpdate={canUpdate}
            onReset={remountAfterReset(SettingName.Signin)}
          />
        )}
        {email && (
          <EmailSettingsCard
            key={`${SettingName.Email}:${resetKeys.Email ?? 0}`}
            settings={email}
            ownSource={ownSource}
            canUpdate={canUpdate}
            onReset={remountAfterReset(SettingName.Email)}
          />
        )}
      </>
    )
  }

  return (
    <div className="flex max-w-3xl flex-col gap-5">
      {banner}
      {content()}
    </div>
  )
}
