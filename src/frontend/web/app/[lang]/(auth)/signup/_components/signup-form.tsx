'use client'

import * as Yup from 'yup'
import { useTranslation } from '@/i18n'
import { Formik, Form } from 'formik'
import { FormInput } from '@/components/ui/form/form-input'
import { useSignupMutation, useResendVerifyEmailMutation } from '@/store/api/identity/account/account-api'
import { Button } from '@/components/ui/button'
import { FormPasswordInput } from '@/components/ui/form/form-password-input'
import { LocalizedLink } from '@/components/localized-link'
import { Mail, Lock } from 'lucide-react'
import { useState, useEffect } from 'react'
import { CheckCircle } from 'lucide-react'
import { successToast } from '@/lib/utils/notification'
import { apiErrorAlert } from '@/lib/utils'

/**
 * Interactive client-side form that registers a new user with username, email, and password.
 * Shows a success view after registration and optionally offers a resend-verification-email countdown when email verification is required.
 */
const SignupForm = () => {
  const { t } = useTranslation()
  const [successMessage, setSuccessMessage] = useState<string | undefined>(undefined)
  const [registeredEmail, setRegisteredEmail] = useState<string>('')
  const [countdown, setCountdown] = useState(0)

  const [signupApi, { isLoading: isSubmittingSignup }] = useSignupMutation()
  const [resendVerifyEmailApi, { isLoading: isResendingVerifyEmail }] = useResendVerifyEmailMutation()

  useEffect(() => {
    let timer: NodeJS.Timeout
    if (countdown > 0) {
      timer = setInterval(() => {
        setCountdown((prev) => prev - 1)
      }, 1000)
    }
    return () => {
      if (timer) clearInterval(timer)
    }
  }, [countdown])

  const validationSchema = Yup.object().shape({
    username: Yup.string()
      .required(t('validation.required'))
      .min(3, t('validation.minLength', { min: 3 }))
      .max(50, t('validation.maxLength', { max: 50 })),
    email: Yup.string()
      .required(t('validation.required'))
      .email(t('validation.email'))
      .max(100, t('validation.maxLength', { max: 100 })),
    password: Yup.string()
      .required(t('validation.required'))
      .min(8, t('validation.minLength', { min: 8 }))
      .max(50, t('validation.maxLength', { max: 50 })),
    confirmPassword: Yup.string()
      .required(t('validation.required'))
      .oneOf([Yup.ref('password')], t('validation.mustMatch', { otherField: t('form.label.password') })),
  })

  type SignupFormValues = Yup.InferType<typeof validationSchema>

  const submitForm = async (values: SignupFormValues) => {
    const response = await signupApi(values)
    if (response.error) {
      apiErrorAlert(response.error)
      return
    }

    setRegisteredEmail(values.email)
    if (response.data?.isEmailVerificationRequired) {
      setSuccessMessage(t('page.auth.signup.successVerifyEmail'))
      setCountdown(15)
    } else {
      setSuccessMessage(t('page.auth.signup.successMessage'))
    }
  }

  const handleResendEmail = async () => {
    if (countdown > 0 || isResendingVerifyEmail) return

    try {
      const response = await resendVerifyEmailApi({ emailOrUsername: registeredEmail })
      if (response.error) {
        apiErrorAlert(response.error)
        return
      }
      successToast.fire({
        text: t('page.verifyEmail.resendSuccess'),
      })
      setCountdown(15)
    } catch {
      // Error is handled by api middleware/toast
    }
  }

  if (successMessage) {
    const isVerificationRequired = successMessage === t('page.auth.signup.successVerifyEmail')

    return (
      <div className="flex flex-col items-center justify-center space-y-4 text-center dark:text-white">
        <CheckCircle size={48} className="text-green-500" />
        <h2 className="text-2xl font-bold">{t('page.auth.signup.successTitle')}</h2>
        <p>{successMessage}</p>

        {isVerificationRequired && (
          <Button
            type="button"
            className="btn btn-outline-primary w-full mt-2"
            onClick={handleResendEmail}
            disabled={countdown > 0 || isResendingVerifyEmail}
            isLoading={isResendingVerifyEmail}
          >
            {countdown > 0 ? t('page.verifyEmail.resendWait', { seconds: countdown }) : t('page.verifyEmail.resendButton')}
          </Button>
        )}

        <LocalizedLink href="/signin" className="btn btn-primary w-full mt-2">
          {t('page.auth.signin.backToSignin')}
        </LocalizedLink>
      </div>
    )
  }

  return (
    <Formik initialValues={{ username: '', email: '', password: '', confirmPassword: '' }} validationSchema={validationSchema} onSubmit={submitForm}>
      {() => (
        <Form className="space-y-5 dark:text-white">
          <FormInput label={t('form.label.username')} name="username" placeholder={t('form.placeholder.username')} icon={<Mail size={16} />} autoFocus={true} required={true} />
          <FormInput label={t('form.label.email')} name="email" placeholder={t('form.placeholder.email')} icon={<Mail size={16} />} required={true} />
          <FormPasswordInput label={t('form.label.password')} name="password" placeholder={t('form.placeholder.password')} icon={<Lock size={16} />} required={true} />
          <FormPasswordInput label={t('form.label.confirmPassword')} name="confirmPassword" placeholder={t('form.placeholder.confirmPassword')} icon={<Lock size={16} />} required={true} />

          <div className="flex justify-end gap-2">
            <span className="text-sm">{t('page.auth.signup.alreadyHaveAccount')}</span>
            <LocalizedLink href="/signin" className="text-sm text-primary hover:underline dark:text-white">
              {t('page.auth.signup.signinLink')}
            </LocalizedLink>
          </div>

          <Button type="submit" className="btn w-full border-0 btn-gradient uppercase shadow-[0_10px_20px_-10px_rgba(67,97,238,0.44)]" isLoading={isSubmittingSignup}>
            {t('page.auth.signup.button')}
          </Button>
        </Form>
      )}
    </Formik>
  )
}

export default SignupForm
