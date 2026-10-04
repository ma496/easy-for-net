'use client'
import * as Yup from 'yup'
import { useTranslation } from '@/i18n'
import { useLocalizedRouter } from '@/hooks'
import { useForgetPasswordMutation } from '@/store/api/identity'
import { Form, Formik } from 'formik'
import { Button, LocalizedLink } from '@/components/ui'
import { FormInput } from '@/components/ui/form'
import { Mail } from 'lucide-react'
import { successAlert, apiErrorAlert } from '@/lib/utils'

/**
 * Builds a Yup validation schema for the forget-password form using the supplied translation function for error messages.
 */
const createValidationSchema = (t: (key: string, params?: Record<string, string | number>) => string) => {
  return Yup.object().shape({
    email: Yup.string()
      .required(t('validation.required'))
      .email(t('validation.invalidEmail')),
  })
}

/**
 * Interactive client-side form that requests a password-reset email for the supplied address.
 * On a successful response, displays a success alert and navigates back to the sign-in page.
 */
export const ForgetPasswordForm = () => {
  const { t } = useTranslation()
  const validationSchema = createValidationSchema(t)
  type ForgetPasswordFormValues = Yup.InferType<typeof validationSchema>
  const [forgetPassword, { isLoading: isForgettingPassword }] = useForgetPasswordMutation()
  const router = useLocalizedRouter()

  const onSubmit = async (data: ForgetPasswordFormValues) => {
    const result = await forgetPassword({
      email: data.email,
    })

    if (result.error) {
      apiErrorAlert(result.error)
      return
    }
    successAlert({
      title: t('page.auth.forgotPassword.success'),
      text: t('page.auth.forgotPassword.checkEmail'),
    })
    router.push('/signin')
  }

  return (
    <Formik
      initialValues={{
        email: '',
      }}
      validationSchema={validationSchema}
      onSubmit={onSubmit}
    >
      {() => (
        <Form noValidate className="space-y-5">
          <FormInput name="email" type="email" label={t('form.label.email')} placeholder={t('form.placeholder.email')} icon={<Mail size={16} />} autoFocus={true} required={true} />
          <Button type="submit" size="lg" className="w-full" isLoading={isForgettingPassword}>
            {t('page.auth.forgotPassword.button')}
          </Button>
          <p className="text-center text-sm">
            <LocalizedLink href="/signin" className="font-medium text-primary hover:underline">
              {t('page.auth.signin.backToSignin')}
            </LocalizedLink>
          </p>
        </Form>
      )}
    </Formik>
  )
}
