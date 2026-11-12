'use client'

import { useState } from 'react'
import { Form, Formik, FormikHelpers } from 'formik'
import { useTranslation } from '@/i18n'
import { FormCheckbox } from '@/components/ui/form'
import { successToast } from '@/lib/utils'
import { SettingName, GeneralSettingsDto, toGeneralSettings, useSettingUpdateMutation } from '@/store/api/settings'
import { Badge } from '@/components/ui'
import { SettingsCard, SourceBadge } from './settings-card-parts'
import { buildGeneralUpdateValues, hasOwnOverride, markerOf, generalFormValues, generalSources, GeneralFormValues } from './settings-form'
import { useSettingReset } from './use-setting-reset'

/** Props for the General card: the resolved setting and whether it may be edited. */
interface GeneralSettingsCardProps {
  settings: GeneralSettingsDto
  canUpdate: boolean
  onReset: () => Promise<void>
}

/**
 * The `General` setting: whether an account must verify its email address before it may sign in, and
 * whether anyone may create an account from the sign-up page.
 */
export const GeneralSettingsCard = ({ settings: initialSettings, canUpdate, onReset }: GeneralSettingsCardProps) => {
  const { t } = useTranslation()
  const title = t('page.settings.general.title') || 'General'
  // What the card shows as resolved: the load it mounted with, then each save's answer.
  const [settings, setSettings] = useState(initialSettings)
  const [updateSetting, { isLoading: isSaving, error: saveError, reset: clearSaveError }] = useSettingUpdateMutation()
  const { reset, isResetting } = useSettingReset(SettingName.General, title, onReset)

  const onSubmit = async (values: GeneralFormValues, { resetForm }: FormikHelpers<GeneralFormValues>) => {
    const result = await updateSetting({
      name: SettingName.General,
      values: buildGeneralUpdateValues(settings, generalFormValues(settings), values),
    })
    if (result.error) return

    const next = toGeneralSettings(result.data) ?? settings
    setSettings(next)
    resetForm({ values: generalFormValues(next) })
    successToast.fire({ text: t('page.settings.saveSuccess', { setting: title }) || `${title} settings saved` })
  }

  const fieldId = 'setting-general-isEmailVerificationRequired'
  const signupFieldId = 'setting-general-isSignupEnabled'

  return (
    <Formik<GeneralFormValues> initialValues={generalFormValues(initialSettings)} onSubmit={onSubmit}>
      {({ values, initialValues, dirty, resetForm }) => (
        <Form noValidate>
          <SettingsCard
            title={title}
            description={t('page.settings.general.description') || 'Options that apply across the application.'}
            isCustomized={hasOwnOverride(generalSources(settings))}
            canUpdate={canUpdate}
            dirty={dirty}
            isSaving={isSaving}
            isResetting={isResetting}
            saveError={saveError}
            onReset={reset}
            onDiscard={() => {
              resetForm()
              clearSaveError()
            }}
          >
            <div>
              <div className="flex flex-wrap items-center justify-between gap-2">
                <FormCheckbox id={fieldId} name="isEmailVerificationRequired" label={t('page.settings.general.isEmailVerificationRequired') || 'Require a verified email address to sign in'} disabled={!canUpdate} />
                <div className="flex flex-wrap items-center gap-1">
                  <SourceBadge marker={markerOf(settings.isEmailVerificationRequired.source)} />
                  {values.isEmailVerificationRequired !== initialValues.isEmailVerificationRequired && (
                    <Badge variant="warning" type="outline">
                      {t('page.settings.changed') || 'Changed'}
                    </Badge>
                  )}
                </div>
              </div>
              <p className="mt-1 text-xs text-muted-foreground">{t('page.settings.general.isEmailVerificationRequiredHelp') || 'When on, an account must confirm its email address before it can sign in.'}</p>
            </div>
            <div>
              <div className="flex flex-wrap items-center justify-between gap-2">
                <FormCheckbox id={signupFieldId} name="isSignupEnabled" label={t('page.settings.general.isSignupEnabled') || 'Allow sign-up'} disabled={!canUpdate} />
                <div className="flex flex-wrap items-center gap-1">
                  <SourceBadge marker={markerOf(settings.isSignupEnabled.source)} />
                  {values.isSignupEnabled !== initialValues.isSignupEnabled && (
                    <Badge variant="warning" type="outline">
                      {t('page.settings.changed') || 'Changed'}
                    </Badge>
                  )}
                </div>
              </div>
              <p className="mt-1 text-xs text-muted-foreground">
                {t('page.settings.general.isSignupEnabledHelp') || 'Let anyone create an account from the sign-up page.'}
              </p>
            </div>
          </SettingsCard>
        </Form>
      )}
    </Formik>
  )
}
