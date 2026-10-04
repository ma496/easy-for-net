'use client'

import { useState } from 'react'
import * as Yup from 'yup'
import { Form, Formik, FormikHelpers } from 'formik'
import { useTranslation } from '@/i18n'
import { Badge, Button } from '@/components/ui'
import { FormInput, FormPasswordInput } from '@/components/ui/form'
import { successToast } from '@/lib/utils'
import { EmailSettingsDto, SettingName, SettingSource, toEmailSettings, useSettingUpdateMutation } from '@/store/api/settings'
import { SettingField, SettingsCard } from './settings-card-parts'
import { buildEmailUpdateValues, emailFormValues, emailSources, EmailFormValues, hasOwnOverride, isValidPort, markerOf, portText } from './settings-form'
import { useSettingReset } from './use-setting-reset'

/** Props for the Email card: the resolved setting, the layer being edited, and whether it may be. */
interface EmailSettingsCardProps {
  settings: EmailSettingsDto
  ownSource: SettingSource
  canUpdate: boolean
  onReset: () => Promise<void>
}

/** Client-side checks only for shape - the API decides what the resolved setting as a whole must hold. */
const createValidationSchema = (t: (key: string, params?: Record<string, string | number>) => string) =>
  Yup.object().shape({
    smtpPort: Yup.mixed<string | number>().test('port', t('page.settings.email.portInvalid'), (value) => isValidPort(value)),
    senderEmail: Yup.string().email(t('validation.invalidEmail')),
  })

/** The text properties of the Email setting, in the order the grid lays them out beside the password. */
type EmailTextField = 'smtpServer' | 'smtpPort' | 'smtpUsername' | 'senderEmail' | 'senderName'

/**
 * The `Email` setting: the SMTP server outgoing mail is delivered through, and the sender it is
 * delivered as. The password is never shown - only whether one is set - and is sent only when typed,
 * or as a clear when the scope's own one is cleared.
 */
export const EmailSettingsCard = ({ settings: initialSettings, ownSource, canUpdate, onReset }: EmailSettingsCardProps) => {
  const { t } = useTranslation()
  const title = t('page.settings.email.title')
  // What the card shows as resolved: the load it mounted with, then each save's answer.
  const [settings, setSettings] = useState(initialSettings)
  const [updateSetting, { isLoading: isSaving, error: saveError, reset: clearSaveError }] = useSettingUpdateMutation()
  const { reset, isResetting } = useSettingReset(SettingName.Email, title, ownSource, onReset)
  const validationSchema = createValidationSchema(t)

  const onSubmit = async (values: EmailFormValues, { resetForm }: FormikHelpers<EmailFormValues>) => {
    const result = await updateSetting({
      name: SettingName.Email,
      values: buildEmailUpdateValues(settings, emailFormValues(settings), values, ownSource),
    })
    if (result.error) return

    const next = toEmailSettings(result.data) ?? settings
    setSettings(next)
    resetForm({ values: emailFormValues(next) })
    successToast.fire({ text: t('page.settings.saveSuccess', { setting: title }) })
  }

  const idOf = (field: keyof EmailFormValues) => `setting-email-${field}`
  const password = settings.smtpPassword
  const passwordMarker = markerOf(password.source, ownSource)
  const canClearPassword = canUpdate && password.isSet && passwordMarker === 'own'

  return (
    <Formik<EmailFormValues> initialValues={emailFormValues(initialSettings)} validationSchema={validationSchema} onSubmit={onSubmit}>
      {({ values, initialValues, dirty, resetForm, setFieldValue }) => {
        const isChanged = (field: EmailTextField) =>
          field === 'smtpPort' ? portText(values.smtpPort) !== portText(initialValues.smtpPort) : values[field] !== initialValues[field]

        /** One text property of the setting, with its label row and source badges. */
        const textField = (field: EmailTextField, label: string, inputProps: React.InputHTMLAttributes<HTMLInputElement> = {}) => (
          <SettingField id={idOf(field)} label={label} marker={markerOf(settings[field].source, ownSource)} changed={isChanged(field)}>
            <FormInput id={idOf(field)} name={field} disabled={!canUpdate} {...inputProps} />
          </SettingField>
        )

        return (
          <Form noValidate>
            <SettingsCard
              title={title}
              description={t('page.settings.email.description')}
              isCustomized={hasOwnOverride(emailSources(settings), ownSource)}
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
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                {textField('smtpServer', t('page.settings.email.smtpServer'), { dir: 'ltr' })}
                {textField('smtpPort', t('page.settings.email.smtpPort'), { dir: 'ltr', type: 'number', inputMode: 'numeric', min: 1, max: 65535 })}
                {textField('smtpUsername', t('page.settings.email.smtpUsername'), { dir: 'ltr', autoComplete: 'off' })}

                <SettingField
                  id={idOf('smtpPassword')}
                  label={t('page.settings.email.smtpPassword')}
                  marker={passwordMarker}
                  changed={values.smtpPassword.length > 0 || values.smtpPasswordCleared}
                  extraBadges={
                    password.isSet ? (
                      <Badge variant="success" type="outline">
                        {t('page.settings.email.passwordSet')}
                      </Badge>
                    ) : (
                      <Badge variant="secondary" type="outline">
                        {t('page.settings.email.passwordNotSet')}
                      </Badge>
                    )
                  }
                >
                  <FormPasswordInput
                    id={idOf('smtpPassword')}
                    name="smtpPassword"
                    autoComplete="new-password"
                    disabled={!canUpdate || values.smtpPasswordCleared}
                    placeholder={password.isSet ? t('page.settings.email.passwordKeepPlaceholder') : t('page.settings.email.passwordNotSetPlaceholder')}
                  />
                  {values.smtpPasswordCleared ? (
                    <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-warning">
                      <span>{t('page.settings.email.passwordWillClear')}</span>
                      <button type="button" className="cursor-pointer font-semibold underline" onClick={() => setFieldValue('smtpPasswordCleared', false)}>
                        {t('page.settings.email.passwordUndo')}
                      </button>
                    </div>
                  ) : (
                    canClearPassword && (
                      <Button
                        type="button"
                        variant="outline-danger"
                        size="sm"
                        className="mt-2"
                        onClick={() => {
                          setFieldValue('smtpPassword', '')
                          setFieldValue('smtpPasswordCleared', true)
                        }}
                      >
                        {t('page.settings.email.passwordClear')}
                      </Button>
                    )
                  )}
                  {canUpdate && <p className="mt-1 text-xs text-muted-foreground">{t('page.settings.email.passwordInheritHint')}</p>}
                </SettingField>

                {textField('senderEmail', t('page.settings.email.senderEmail'), { dir: 'ltr', type: 'email' })}
                {textField('senderName', t('page.settings.email.senderName'))}
              </div>
            </SettingsCard>
          </Form>
        )
      }}
    </Formik>
  )
}
