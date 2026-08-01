'use client'
import * as Yup from 'yup'
import { useTranslation } from '@/i18n'
import { useLocalizedRouter } from '@/hooks/use-localized-router'
import { useChangePasswordMutation } from '@/store/api/identity/account/account-api'
import { useAppDispatch } from '@/store/hooks'
import { signout } from '@/store/slices/authSlice'
import { apiErrorAlert, successToast } from '@/lib/utils'
import { Form, Formik } from 'formik'
import { FormPasswordInput } from '@/components/ui/form'
import { Button } from '@/components/ui'
import { Lock } from 'lucide-react'

/**
 * Builds a Yup validation schema for the change-password form using the supplied translation function for error messages.
 */
const createValidationSchema = (t: (key: string, params?: Record<string, string | number>) => string) => {
  return Yup.object().shape({
    currentPassword: Yup.string()
      .required(t('validation.required')),
    newPassword: Yup.string()
      .required(t('validation.required'))
      .min(8, t('validation.minLength', { min: 8 }))
      .max(50, t('validation.maxLength', { max: 50 })),
    confirmPassword: Yup.string()
      .required(t('validation.required'))
      .oneOf([Yup.ref('newPassword')], t('validation.mustMatch', { otherField: t('form.label.newPassword') })),
  })
}

/**
 * Interactive client-side form that lets an authenticated user change their account password.
 * Submits the new credentials, signs the user out on success, and redirects to the sign-in page.
 */
export const ChangePasswordForm = () => {
  const { t } = useTranslation()
  const validationSchema = createValidationSchema(t)
  type ChangePasswordFormValues = Yup.InferType<typeof validationSchema>
  const [changePassword, { isLoading: isChangingPassword }] = useChangePasswordMutation()
  const router = useLocalizedRouter()
  const dispatch = useAppDispatch()

  const onSubmit = async (data: ChangePasswordFormValues) => {
    const result = await changePassword({
      currentPassword: data.currentPassword,
      newPassword: data.newPassword,
    })

    if (result.error) {
      apiErrorAlert(result.error)
      return
    }

    successToast.fire({
      text: t('page.profile.changePasswordSuccess'),
    })
    dispatch(signout())
    router.push('/signin')
  }

  return (
    <Formik
      initialValues={{
        currentPassword: '',
        newPassword: '',
        confirmPassword: '',
      }}
      validationSchema={validationSchema}
      onSubmit={onSubmit}
    >
      {() => (
        <Form noValidate className="flex flex-col gap-4">
          <FormPasswordInput name="currentPassword" label={t('form.label.currentPassword')} placeholder={t('form.placeholder.currentPassword')} icon={<Lock size={18} />} autoFocus={true} required={true} />
          <FormPasswordInput name="newPassword" label={t('form.label.newPassword')} placeholder={t('form.placeholder.newPassword')} icon={<Lock size={18} />} required={true} />
          <FormPasswordInput name="confirmPassword" label={t('form.label.confirmPassword')} placeholder={t('form.placeholder.confirmPassword')} icon={<Lock size={18} />} required={true} />
          <div className="flex justify-end">
            <Button type="submit" isLoading={isChangingPassword}>
              {t('common.submit')}
            </Button>
          </div>
        </Form>
      )}
    </Formik>
  )
}
