'use client'

import * as Yup from 'yup'
import { useTranslation } from '@/i18n'
import { Formik, Form } from 'formik'
import { FormInput, FormPasswordInput } from '@/components/ui/form'
import { useSignupMutation, useResendVerifyEmailMutation } from '@/store/api/identity'
import { Button, LocalizedLink } from '@/components/ui'
import { useState, useEffect, ReactNode } from 'react'
import { Mail, Lock, CheckCircle, User } from 'lucide-react'
import { apiErrorAlert, getErrorCode, successToast } from '@/lib/utils'

/** What the success view needs once the account exists. */
interface SignupResult {
  message: string
  isVerificationRequired: boolean
}

/** Props for the centred block that replaces the form when sign-up is closed. */
interface SignupNoticeProps {
  title: string
  message: ReactNode
  icon: ReactNode
  children?: ReactNode
}

/** A centred icon tile, heading and message in place of the form, with the ways on beneath it. */
const SignupNotice = ({ title, message, icon, children }: SignupNoticeProps) => (
  <div className="flex flex-col items-center text-center">
    <div className="mb-5 flex size-12 items-center justify-center rounded-xl bg-surface-2 text-muted-foreground">{icon}</div>
    <h1 className="text-2xl font-semibold tracking-tight wrap-break-word text-foreground">{title}</h1>
    <p className="mt-2 text-sm wrap-break-word text-muted-foreground">{message}</p>
    <div className="mt-6 flex w-full flex-col gap-2">{children}</div>
  </div>
)

/** The "sign-up is closed" notice, linking to sign-in: what the route renders while the site has sign-up switched off. */
export const SignupClosedNotice = () => {
  const { t } = useTranslation()

  return (
    <SignupNotice
      title={t('page.auth.signup.disabledTitle') || 'Sign-up is closed'}
      message={t('page.auth.signup.disabledMessage') || 'New accounts are created by an administrator. If you already have an account, sign in.'}
      icon={<Lock className="size-6" />}
    >
      <LocalizedLink href="/signin" className="btn btn-primary w-full">
        {t('page.auth.signup.disabledAction') || 'Go to sign in'}
      </LocalizedLink>
    </SignupNotice>
  )
}

/**
 * Interactive client-side form that registers a new visitor account. The success view links to sign-in,
 * and offers a resend-verification-email countdown when email verification is required. The route shows
 * {@link SignupClosedNotice} instead of this form while sign-up is switched off; should it be switched
 * off after the page was served, the API answers 403 `signupDisabled` and the form gives way to the same notice.
 */
export const SignupForm = () => {
  const { t } = useTranslation()
  const [isClosed, setIsClosed] = useState(false)
  const [result, setResult] = useState<SignupResult | undefined>(undefined)
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
  })

  type SignupFormValues = Yup.InferType<typeof validationSchema>

  const submitForm = async (values: SignupFormValues) => {
    const response = await signupApi(values)
    if (response.error) {
      if (getErrorCode(response.error) === 'signupDisabled') {
        setIsClosed(true)
        return
      }
      apiErrorAlert(response.error)
      return
    }

    setRegisteredEmail(values.email)
    const isVerificationRequired = !!response.data?.isEmailVerificationRequired
    setResult({
      isVerificationRequired,
      message: isVerificationRequired
        ? t('page.auth.signup.successVerifyEmail') || 'Registration successful! Please check your email to verify your account.'
        : t('page.auth.signup.successMessage') || 'Registration successful! Go to sign in page.',
    })
    if (isVerificationRequired) setCountdown(15)
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

  if (isClosed) {
    // Sign-up was switched off after the page was served: the API refused the form with 403 signupDisabled.
    return <SignupClosedNotice />
  }

  if (result) {
    return (
      <div className="flex flex-col items-center text-center">
        <div className="mb-5 flex size-12 items-center justify-center rounded-xl bg-success/10 text-success">
          <CheckCircle className="size-6" />
        </div>
        <h1 className="text-2xl font-semibold tracking-tight wrap-break-word text-foreground">
          {t('page.auth.signup.successTitle') || 'Success!'}
        </h1>
        <p className="mt-2 text-sm text-muted-foreground">{result.message}</p>
        <div className="mt-6 flex w-full flex-col gap-2">
          {result.isVerificationRequired && (
            <Button type="button" variant="outline" className="w-full" onClick={handleResendEmail} disabled={countdown > 0 || isResendingVerifyEmail} isLoading={isResendingVerifyEmail}>
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
    <>
      {/* The form titles itself, as sign-in does, so the success view's heading is the page's only h1 rather than one beneath "Sign Up". */}
      <div className="mb-8">
        <h1 className="text-2xl font-semibold tracking-tight wrap-break-word text-foreground">{t('page.auth.signup.title') || 'Sign Up'}</h1>
        <p className="mt-1.5 text-sm wrap-break-word text-muted-foreground">{t('page.auth.signup.description') || 'Enter your email and password to register'}</p>
      </div>
      <Formik initialValues={{ username: '', email: '', password: '', confirmPassword: '' }} validationSchema={validationSchema} onSubmit={submitForm}>
        {() => (
          <Form className="space-y-5">
            <FormInput
              label={t('form.label.username') || 'Username'}
              name="username"
              placeholder={t('form.placeholder.username') || 'Enter username'}
              icon={<User size={16} />}
              autoFocus={true}
              required={true}
            />
            <FormInput label={t('form.label.email') || 'Email'} name="email" placeholder={t('form.placeholder.email') || 'Enter email'} icon={<Mail size={16} />} required={true} />
            <FormPasswordInput
              label={t('form.label.password') || 'Password'}
              name="password"
              placeholder={t('form.placeholder.password') || 'Enter password'}
              icon={<Lock size={16} />}
              required={true}
            />
            <FormPasswordInput
              label={t('form.label.confirmPassword') || 'Confirm Password'}
              name="confirmPassword"
              placeholder={t('form.placeholder.confirmPassword') || 'Enter confirm password'}
              icon={<Lock size={16} />}
              required={true}
            />

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
    </>
  )
}
