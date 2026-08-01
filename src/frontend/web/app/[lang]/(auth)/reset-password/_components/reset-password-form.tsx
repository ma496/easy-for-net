'use client'
import * as Yup from 'yup'
import { useTranslation } from '@/i18n'
import { useSearchParams } from 'next/navigation'
import { useLocalizedRouter } from '@/hooks/use-localized-router'
import { useResetPasswordMutation } from '@/store/api/identity'
import { Form, Formik } from 'formik'
import { Button } from '@/components/ui'
import { FormPasswordInput } from '@/components/ui/form'
import { Lock } from 'lucide-react'
import { apiErrorAlert, errorAlert, successAlert } from '@/lib/utils'

/**
 * Builds a Yup validation schema for the reset-password form using the supplied translation function for error messages.
 */
const createValidationSchema = (t: (key: string, params?: Record<string, string | number>) => string) => {
  return Yup.object().shape({
    password: Yup.string()
      .required(t('validation.required'))
      .min(8, t('validation.minLength', { min: 8 }))
      .max(50, t('validation.maxLength', { max: 50 })),
    confirmPassword: Yup.string()
      .required(t('validation.required'))
      .oneOf([Yup.ref('password')], t('validation.mustMatch', { otherField: t('form.label.newPassword') })),
  })
}

/**
 * Interactive client-side form that completes a password reset using the token from the URL query string.
 * Validates the new password/confirmation, calls the reset API, and redirects to the sign-in page on success.
 */
export const ResetPasswordForm = () => {
  const { t } = useTranslation()
  const validationSchema = createValidationSchema(t)
  type ResetPasswordFormValues = Yup.InferType<typeof validationSchema>
  const [resetPassword, { isLoading: isResettingPassword }] = useResetPasswordMutation()
  const router = useLocalizedRouter()
  const searchParams = useSearchParams()
  const token = searchParams.get('token')

  const onSubmit = async (data: ResetPasswordFormValues) => {
    if (!token) {
      errorAlert({
        text: t('error.server.invalidToken'),
      })
      return
    }

    const result = await resetPassword({
      token,
      password: data.password,
    })

    if (result.error) {
      apiErrorAlert(result.error)
      return
    }

    successAlert({
      title: t('page.auth.resetPassword.success'),
      text: t('page.auth.resetPassword.hasBeenReset'),
    })
    router.push('/signin')
  }

  return (
    <div className="panel flex min-w-75 flex-col gap-4 sm:min-w-125">
      <Formik
        initialValues={{
          password: '',
          confirmPassword: '',
        }}
        validationSchema={validationSchema}
        onSubmit={onSubmit}
      >
        {() => (
          <Form noValidate className="flex flex-col gap-4">
            <FormPasswordInput name="password" label={t('form.label.newPassword')} placeholder={t('form.placeholder.newPassword')} icon={<Lock size={16} />} autoFocus={true} required={true} />
            <FormPasswordInput name="confirmPassword" label={t('form.label.confirmPassword')} placeholder={t('form.placeholder.confirmPassword')} icon={<Lock size={16} />} required={true} />
            <div className="flex justify-end">
              <Button type="submit" isLoading={isResettingPassword}>
                {t('page.auth.resetPassword.button')}
              </Button>
            </div>
          </Form>
        )}
      </Formik>
    </div>
  )
}
