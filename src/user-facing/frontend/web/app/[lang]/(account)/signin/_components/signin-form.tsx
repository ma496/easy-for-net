'use client'
import * as Yup from 'yup'
import { useTranslation } from '@/i18n'
import { Formik, Form } from 'formik'
import { FormInput, FormPasswordInput } from '@/components/ui/form'
import { User, Lock, AlertCircle } from 'lucide-react'
import { useState, useEffect } from 'react'
import { useSearchParams } from 'next/navigation'
import { useTokenMutation, useLazyGetUserInfoQuery, useResendVerifyEmailMutation } from '@/store/api/identity'
import { Button, LocalizedLink } from '@/components/ui'
import { useLocalizedRouter } from '@/hooks'
import { apiErrorAlert, getErrorCode, isAllowed, successToast } from '@/lib/utils'
import { enterSignedIn, resolveSigninLanding } from '@/lib/utils/redirect'
import { Allow } from '@/allow'

/**
 * Interactive client-side form that authenticates a user with username/password and sends them on: to
 * the `redirect` they were sent here with, else to the admin dashboard when they may open it, else to the
 * home page. Manages a verification-message sub-state with a resend-email countdown for accounts whose
 * email is not yet verified. The "Don't have an account?" link to sign-up shows only while the site has
 * sign-up open, as the route asked the API on the server.
 */
export const SigninForm = ({ isSignupEnabled = true }: { isSignupEnabled?: boolean }) => {
  const router = useLocalizedRouter()
  const { t } = useTranslation()
  const searchParams = useSearchParams()
  const redirectTo = searchParams.get('redirect')

  const validationSchema = Yup.object().shape({
    username: Yup.string()
      .required(t('validation.required') || 'Field is required')
      .min(3, t('validation.minLength', { min: 3 }) || `Field must be at least ${3} characters`)
      .max(50, t('validation.maxLength', { max: 50 }) || `Field must not exceed ${50} characters`),
    password: Yup.string()
      .required(t('validation.required') || 'Field is required')
      .min(8, t('validation.minLength', { min: 8 }) || `Field must be at least ${8} characters`)
      .max(50, t('validation.maxLength', { max: 50 }) || `Field must not exceed ${50} characters`),
  })

  type SigninFormValues = Yup.InferType<typeof validationSchema>

  const [tokenApi, { isLoading: isTokenLoading }] = useTokenMutation()
  const [getUserInfo, { isLoading: isLoadingUserInfo }] = useLazyGetUserInfoQuery()
  const [resendVerifyEmailApi, { isLoading: isResending }] = useResendVerifyEmailMutation()

  const [showResendLink, setShowResendLink] = useState(false)
  const [registeredEmail, setRegisteredEmail] = useState('')
  const [countdown, setCountdown] = useState(0)
  const [isEntering, setIsEntering] = useState(false)

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

  const submitForm = async (values: SigninFormValues) => {
    const tokenRes = await tokenApi(values)
    if (tokenRes.error) {
      const code = getErrorCode(tokenRes.error)
      if (code === 'emailNotVerified') {
        setRegisteredEmail(values.username)
        setShowResendLink(true)
        return
      }
      apiErrorAlert(tokenRes.error)
      return
    }

    const userInfoRes = await getUserInfo()
    if (userInfoRes.error) {
      apiErrorAlert(userInfoRes.error)
      return
    }
    if (userInfoRes.data) {
      const canAccessAdmin = isAllowed({ user: userInfoRes.data, isAuthenticated: true }, [Allow.Admin_Access])
      const landing = resolveSigninLanding(redirectTo, canAccessAdmin)

      // A full page load, never `router.push`: see `enterSignedIn`. The new document also re-fetches
      // the root layout's texts and languages, and reads the account
      // info for its own store. The form stays busy until the browser leaves, so it cannot be sent twice.
      setIsEntering(true)
      enterSignedIn(router.localize(landing))
    }
  }

  const handleResendEmail = async () => {
    if (countdown > 0 || isResending) return

    const response = await resendVerifyEmailApi({ emailOrUsername: registeredEmail })
    if (response.error) {
      apiErrorAlert(response.error)
      return
    }
    successToast.fire({
      text: t('page.verifyEmail.resendSuccess') || 'Verification email has been resent.',
    })
    setCountdown(15)
  }

  return (
    <>
      <div className="mb-8">
        <h1 className="text-2xl font-semibold tracking-tight wrap-break-word text-foreground">{t('page.auth.signin.title') || 'Sign In'}</h1>
        <p className="mt-1.5 text-sm wrap-break-word text-muted-foreground">{t('page.auth.signin.description') || 'Enter your credentials to sign in'}</p>
      </div>

      <Formik initialValues={{ username: '', password: '' }} validationSchema={validationSchema} onSubmit={submitForm}>
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
            <FormPasswordInput
              label={t('form.label.password') || 'Password'}
              name="password"
              placeholder={t('form.placeholder.password') || 'Enter password'}
              icon={<Lock size={16} />}
              required={true}
            />

            {showResendLink && (
              <div role="alert" className="flex items-start gap-3 rounded-lg border border-danger/30 bg-danger/10 p-4 text-sm">
                <AlertCircle className="mt-0.5 size-4 shrink-0 text-danger" />
                <div className="min-w-0 flex-1 space-y-2">
                  <p className="font-semibold text-danger">{t('page.verifyEmail.notVerifiedTitle') || 'Email Verification Required'}</p>
                  <p className="text-foreground/80">
                    {t('page.verifyEmail.notVerifiedMessage') || 'Your email address is not verified. Please check your inbox for the verification link or click the button below to resend it.'}
                  </p>
                  <Button type="button" variant="outline" size="sm" onClick={handleResendEmail} disabled={countdown > 0 || isResending} isLoading={isResending}>
                    {countdown > 0 ? t('page.verifyEmail.resendWait', { seconds: countdown }) || `Please wait ${countdown}s` : t('page.verifyEmail.resendButton') || 'Resend Verification Email'}
                  </Button>
                </div>
              </div>
            )}

            <div className="flex justify-end">
              <LocalizedLink href="/forget-password" className="text-sm font-medium text-primary hover:underline">
                {t('page.auth.signin.forgotPassword') || 'Forgot Password?'}
              </LocalizedLink>
            </div>

            <Button type="submit" size="lg" className="w-full" isLoading={isTokenLoading || isLoadingUserInfo || isEntering} disabled={isEntering}>
              {t('page.auth.signin.button') || 'Sign In'}
            </Button>

            {isSignupEnabled && (
              <p className="text-center text-sm text-muted-foreground">
                {t('page.auth.signin.noAccount') || "Don't have an account?"}{' '}
                <LocalizedLink href="/signup" className="font-medium text-primary hover:underline">
                  {t('page.auth.signin.signupLink') || 'Sign Up'}
                </LocalizedLink>
              </p>
            )}
          </Form>
        )}
      </Formik>
    </>
  )
}
