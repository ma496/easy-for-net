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
      .required(t('validation.required') || 'Field is required')
      .min(3, t('validation.minLength', { min: 3 }) || `Field must be at least ${3} characters`)
      .max(50, t('validation.maxLength', { max: 50 }) || `Field must not exceed ${50} characters`),
    email: Yup.string()
      .required(t('validation.required') || 'Field is required')
      .email(t('validation.email') || 'Invalid email format')
      .max(100, t('validation.maxLength', { max: 100 }) || `Field must not exceed ${100} characters`),
    password: Yup.string()
      .required(t('validation.required') || 'Field is required')
      .min(8, t('validation.minLength', { min: 8 }) || `Field must be at least ${8} characters`)
      .max(50, t('validation.maxLength', { max: 50 }) || `Field must not exceed ${50} characters`),
    confirmPassword: Yup.string()
      .required(t('validation.required') || 'Field is required')
      .oneOf([Yup.ref('password')], t('validation.mustMatch', { otherField: t('form.label.password') || 'Password' }) || `Field must match with ${t('form.label.password') || 'Password'}`),
    // Signing up creates the tenant the account will work in: an account belonging to none could
    // exercise no permission at all, so there is no useful half-way state to leave somebody in. The
    // bounds mirror the API's own tenant rules, so the form refuses what the server would refuse.
    tenantName: Yup.string()
      .trim()
      .required(t('validation.required') || 'Field is required')
      .min(2, t('validation.minLength', { min: 2 }) || `Field must be at least ${2} characters`)
      .max(100, t('validation.maxLength', { max: 100 }) || `Field must not exceed ${100} characters`),
    tenantIdentifier: Yup.string()
      .trim()
      .required(t('validation.required') || 'Field is required')
      .min(3, t('validation.minLength', { min: 3 }) || `Field must be at least ${3} characters`)
      .max(50, t('validation.maxLength', { max: 50 }) || `Field must not exceed ${50} characters`)
      .matches(/^[a-z0-9]+(?:-[a-z0-9]+)*$/, t('validation.tenantIdentifier') || 'Use lower-case letters, digits and single hyphens, starting and ending with a letter or digit'),
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
      setSuccessMessage(t('page.auth.signup.successVerifyEmail') || 'Registration successful! Please check your email to verify your account.')
      setCountdown(15)
    } else {
      setSuccessMessage(t('page.auth.signup.successMessage') || 'Registration successful! Go to sign in page.')
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
        text: t('page.verifyEmail.resendSuccess') || 'Verification email has been resent.',
      })
      setCountdown(15)
    } catch {
      // Error is handled by api middleware/toast
    }
  }

  if (successMessage) {
    const isVerificationRequired = successMessage === (t('page.auth.signup.successVerifyEmail') || 'Registration successful! Please check your email to verify your account.')

    return (
      <div className="flex flex-col items-center text-center">
        <div className="mb-5 flex size-12 items-center justify-center rounded-xl bg-success/10 text-success">
          <CheckCircle className="size-6" />
        </div>
        <h2 className="text-xl font-semibold tracking-tight">{t('page.auth.signup.successTitle') || 'Success!'}</h2>
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
              {countdown > 0 ? t('page.verifyEmail.resendWait', { seconds: countdown }) || `Please wait ${countdown}s` : t('page.verifyEmail.resendButton') || 'Resend Verification Email'}
            </Button>
          )}

          <LocalizedLink href="/signin" className="btn btn-primary w-full">
            {t('page.auth.signin.backToSignin') || 'Back to Sign In'}
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
          <FormInput label={t('form.label.username') || 'Username'} name="username" placeholder={t('form.placeholder.username') || 'Enter username'} icon={<Mail size={16} />} autoFocus={true} required={true} />
          <FormInput label={t('form.label.email') || 'Email'} name="email" placeholder={t('form.placeholder.email') || 'Enter email'} icon={<Mail size={16} />} required={true} />
          <FormPasswordInput label={t('form.label.password') || 'Password'} name="password" placeholder={t('form.placeholder.password') || 'Enter password'} icon={<Lock size={16} />} required={true} />
          <FormPasswordInput label={t('form.label.confirmPassword') || 'Confirm Password'} name="confirmPassword" placeholder={t('form.placeholder.confirmPassword') || 'Enter confirm password'} icon={<Lock size={16} />} required={true} />

          <div className="border-t border-border pt-5">
            <p className="mb-4 text-[11px] font-semibold tracking-wider text-subtle-foreground uppercase">{t('page.auth.signup.tenantSectionTitle') || 'Your tenant'}</p>
            <div className="space-y-5">
              <FormInput label={t('form.label.tenantName') || 'Tenant Name'} name="tenantName" placeholder={t('form.placeholder.tenantName') || 'Enter tenant name'} icon={<Building2 size={16} />} required={true} />
              <FormInput label={t('form.label.tenantIdentifier') || 'Identifier'} name="tenantIdentifier" placeholder={t('form.placeholder.tenantIdentifier') || 'Enter identifier (for example acme)'} icon={<Building2 size={16} />} required={true} />
            </div>
          </div>

          <Button type="submit" size="lg" className="w-full" isLoading={isSubmittingSignup}>
            {t('page.auth.signup.button') || 'Sign Up'}
          </Button>

          <p className="text-center text-sm text-muted-foreground">
            {t('page.auth.signup.alreadyHaveAccount') || 'Already have an account?'}{' '}
            <LocalizedLink href="/signin" className="font-medium text-primary hover:underline">
              {t('page.auth.signup.signinLink') || 'Sign In'}
            </LocalizedLink>
          </p>
        </Form>
      )}
    </Formik>
  )
}

