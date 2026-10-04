'use client'

import { useState } from 'react'
import { Form, Formik, FormikHelpers } from 'formik'
import { useTranslation } from '@/i18n'
import { FormCheckbox } from '@/components/ui/form'
import { successToast } from '@/lib/utils'
import { SettingName, SettingSource, SigninSettingsDto, toSigninSettings, useSettingUpdateMutation } from '@/store/api/settings'
import { Badge } from '@/components/ui'
import { SettingsCard, SourceBadge } from './settings-card-parts'
import { buildSigninUpdateValues, hasOwnOverride, markerOf, signinFormValues, signinSources, SigninFormValues } from './settings-form'
import { useSettingReset } from './use-setting-reset'

/** Props for the Sign-in card: the resolved setting, the layer being edited, and whether it may be. */
interface SigninSettingsCardProps {
  settings: SigninSettingsDto
  ownSource: SettingSource
  canUpdate: boolean
  onReset: () => Promise<void>
}

/** The `Signin` setting: whether an account must verify its email address before it may sign in. */
export const SigninSettingsCard = ({ settings: initialSettings, ownSource, canUpdate, onReset }: SigninSettingsCardProps) => {
  const { t } = useTranslation()
  const title = t('page.settings.signin.title')
  // What the card shows as resolved: the load it mounted with, then each save's answer.
  const [settings, setSettings] = useState(initialSettings)
  const [updateSetting, { isLoading: isSaving, error: saveError, reset: clearSaveError }] = useSettingUpdateMutation()
  const { reset, isResetting } = useSettingReset(SettingName.Signin, title, ownSource, onReset)

  const onSubmit = async (values: SigninFormValues, { resetForm }: FormikHelpers<SigninFormValues>) => {
    const result = await updateSetting({
      name: SettingName.Signin,
      values: buildSigninUpdateValues(settings, signinFormValues(settings), values, ownSource),
    })
    if (result.error) return

    const next = toSigninSettings(result.data) ?? settings
    setSettings(next)
    resetForm({ values: signinFormValues(next) })
    successToast.fire({ text: t('page.settings.saveSuccess', { setting: title }) })
  }

  const fieldId = 'setting-signin-isEmailVerificationRequired'

  return (
    <Formik<SigninFormValues> initialValues={signinFormValues(initialSettings)} onSubmit={onSubmit}>
      {({ values, initialValues, dirty, resetForm }) => (
        <Form noValidate>
          <SettingsCard
            title={title}
            description={t('page.settings.signin.description')}
            isCustomized={hasOwnOverride(signinSources(settings), ownSource)}
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
                <FormCheckbox id={fieldId} name="isEmailVerificationRequired" label={t('page.settings.signin.isEmailVerificationRequired')} disabled={!canUpdate} />
                <div className="flex flex-wrap items-center gap-1">
                  <SourceBadge marker={markerOf(settings.isEmailVerificationRequired.source, ownSource)} />
                  {values.isEmailVerificationRequired !== initialValues.isEmailVerificationRequired && (
                    <Badge variant="warning" type="outline">
                      {t('page.settings.changed')}
                    </Badge>
                  )}
                </div>
              </div>
              <p className="mt-1 text-xs text-muted-foreground">{t('page.settings.signin.isEmailVerificationRequiredHelp')}</p>
            </div>
          </SettingsCard>
        </Form>
      )}
    </Formik>
  )
}
