'use client'

import * as Yup from 'yup'
import { useTranslation } from '@/i18n'
import { Formik, Form } from 'formik'
import { FormInput, FormPasswordInput } from '@/components/ui/form'
import { useSignupMutation, useResendVerifyEmailMutation } from '@/store/api/identity'
import { Button, LocalizedLink } from '@/components/ui'
import { useState, useEffect } from 'react'
import { Mail, Lock, CheckCircle, Building2 } from 'lucide-react'
import { apiErrorAlert, successToast } from '@/lib/utils'

/**
 * Interactive client-side form that registers a new user together with the tenant they will work in.
 * Shows a success view after registration and optionally offers a resend-verification-email countdown when email verification is required.
 */
export const SignupForm = () => {
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
    // Signing up creates the tenant the account will work in: an account belonging to none could
    // exercise no permission at all, so there is no useful half-way state to leave somebody in. The
    // bounds mirror the API's own tenant rules, so the form refuses what the server would refuse.
    tenantName: Yup.string()
      .trim()
      .required(t('validation.required'))
      .min(2, t('validation.minLength', { min: 2 }))
      .max(100, t('validation.maxLength', { max: 100 })),
    tenantIdentifier: Yup.string()
      .trim()
      .required(t('validation.required'))
      .min(3, t('validation.minLength', { min: 3 }))
      .max(50, t('validation.maxLength', { max: 50 }))
      .matches(/^[a-z0-9]+(?:-[a-z0-9]+)*$/, t('validation.tenantIdentifier')),
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
      <div className="flex flex-col items-center text-center">
        <div className="mb-5 flex size-12 items-center justify-center rounded-xl bg-success/10 text-success">
          <CheckCircle className="size-6" />
        </div>
        <h2 className="text-xl font-semibold tracking-tight">{t('page.auth.signup.successTitle')}</h2>
        <p className="mt-2 text-sm text-muted-foreground">{successMessage}</p>
        <div className="mt-6 flex w-full flex-col gap-2">
          {isVerificationRequired && (
            <Button
              type="button"
              variant="outline"
              className="w-full"
              onClick={handleResendEmail}
              disabled={countdown > 0 || isResendingVerifyEmail}
              isLoading={isResendingVerifyEmail}
            >
              {countdown > 0 ? t('page.verifyEmail.resendWait', { seconds: countdown }) : t('page.verifyEmail.resendButton')}
            </Button>
          )}

          <LocalizedLink href="/signin" className="btn btn-primary w-full">
            {t('page.auth.signin.backToSignin')}
          </LocalizedLink>
        </div>
      </div>
    )
  }

  return (
    <Formik
      initialValues={{ username: '', email: '', password: '', confirmPassword: '', tenantName: '', tenantIdentifier: '' }}
      validationSchema={validationSchema}
      onSubmit={submitForm}
    >
      {() => (
        <Form className="space-y-5">
          <FormInput label={t('form.label.username')} name="username" placeholder={t('form.placeholder.username')} icon={<Mail size={16} />} autoFocus={true} required={true} />
          <FormInput label={t('form.label.email')} name="email" placeholder={t('form.placeholder.email')} icon={<Mail size={16} />} required={true} />
          <FormPasswordInput label={t('form.label.password')} name="password" placeholder={t('form.placeholder.password')} icon={<Lock size={16} />} required={true} />
          <FormPasswordInput label={t('form.label.confirmPassword')} name="confirmPassword" placeholder={t('form.placeholder.confirmPassword')} icon={<Lock size={16} />} required={true} />

          <div className="border-t border-border pt-5">
            <p className="mb-4 text-[11px] font-semibold tracking-wider text-subtle-foreground uppercase">{t('page.auth.signup.tenantSectionTitle')}</p>
            <div className="space-y-5">
              <FormInput label={t('form.label.tenantName')} name="tenantName" placeholder={t('form.placeholder.tenantName')} icon={<Building2 size={16} />} required={true} />
              <FormInput label={t('form.label.tenantIdentifier')} name="tenantIdentifier" placeholder={t('form.placeholder.tenantIdentifier')} icon={<Building2 size={16} />} required={true} />
            </div>
          </div>

          <Button type="submit" size="lg" className="w-full" isLoading={isSubmittingSignup}>
            {t('page.auth.signup.button')}
          </Button>

          <p className="text-center text-sm text-muted-foreground">
            {t('page.auth.signup.alreadyHaveAccount')}{' '}
            <LocalizedLink href="/signin" className="font-medium text-primary hover:underline">
              {t('page.auth.signup.signinLink')}
            </LocalizedLink>
          </p>
        </Form>
      )}
    </Formik>
  )
}

