'use client'
import * as Yup from 'yup'
import { useTranslation } from '@/i18n'
import { Formik, Form } from 'formik'
import { FormInput, FormPasswordInput } from '@/components/ui/form'
import { Mail, Lock, AlertCircle, Building2 } from 'lucide-react'
import { useState, useEffect } from 'react'
import { useSearchParams } from 'next/navigation'
import { useTokenMutation, useLazyGetUserInfoQuery, useResendVerifyEmailMutation } from '@/store/api/identity'
import { useAppDispatch } from '@/store/hooks'
import { setUserInfo } from '@/store/slices'
import { Button, LocalizedLink } from '@/components/ui'
import { useLocalizedRouter } from '@/hooks'
import { apiErrorAlert, resolvePlatformLanding, resolveTenantLanding, successToast } from '@/lib/utils'
import { isValidRedirectPath } from '@/lib/utils/redirect'

/**
 * Interactive client-side form that authenticates a user with username/password and routes them to the appropriate landing page.
 * Where that is depends on the tenants the account may work in: straight on to the intended screen when the server already made a
 * tenant active, to the chooser when no selection stands but there are tenants to pick from, and to the no-tenant screen
 * when the account belongs to none. Naming a tenant on the form settles it up front, so a person who belongs to several and
 * knows which one they came to work in skips the chooser; naming one they cannot act in refuses the sign-in rather than
 * quietly starting them somewhere else.
 * Manages a verification-message sub-state with a resend-email countdown for accounts whose email is not yet verified.
 */
export const SigninForm = () => {
  const router = useLocalizedRouter()
  const { t } = useTranslation()
  const searchParams = useSearchParams()
  const redirectTo = searchParams.get('redirect')

  const validationSchema = Yup.object().shape({
    username: Yup.string()
      .required(t('validation.required'))
      .min(3, t('validation.minLength', { min: 3 }))
      .max(50, t('validation.maxLength', { max: 50 })),
    password: Yup.string()
      .required(t('validation.required'))
      .min(8, t('validation.minLength', { min: 8 }))
      .max(50, t('validation.maxLength', { max: 50 })),
    // Optional, and only bounded here: which tenants this account may start a session in is the
    // server's question, and an identifier of the wrong shape simply names no tenant.
    tenantIdentifier: Yup.string().max(50, t('validation.maxLength', { max: 50 })),
  })

  type SigninFormValues = Yup.InferType<typeof validationSchema>

  const [tokenApi, { isLoading: isTokenLoading }] = useTokenMutation()
  const [getUserInfo, { isLoading: isLoadingUserInfo }] = useLazyGetUserInfoQuery()
  const [resendVerifyEmailApi, { isLoading: isResending }] = useResendVerifyEmailMutation()
  const dispatch = useAppDispatch()

  const [showResendLink, setShowResendLink] = useState(false)
  const [registeredEmail, setRegisteredEmail] = useState('')
  const [countdown, setCountdown] = useState(0)

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
    // Sent only when it was actually filled in, so an untouched field produces exactly the request a
    // sign-in has always made rather than one naming an empty tenant.
    const tokenRes = await tokenApi({ ...values, tenantIdentifier: values.tenantIdentifier?.trim() || undefined })
    if (tokenRes.error) {
      console.log('tokenRes.error', tokenRes.error)
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const errorData = (tokenRes.error as any).data
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const verificationError = errorData?.errors?.find((err: any) => err.code === 'emailNotVerified')
      if (verificationError) {
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
      dispatch(setUserInfo(userInfoRes.data))

      // Sign-in only establishes an active tenant when the answer is unambiguous: an account with a
      // single usable membership is put into it by the server, and lands on the screen it asked for.
      // An account with several and no choice made yet has no active tenant, and one with none at all
      // never will here, so both are sent to their landing screen instead - ahead of the `redirect`
      // parameter, since honouring it would open a tenant-scoped screen with no tenant behind it.
      // A platform administrator acting in no tenant is the exception: they work platform-wide and need none.
      const validRedirect = redirectTo && isValidRedirectPath(redirectTo) ? redirectTo : null
      const platformLanding = resolvePlatformLanding(userInfoRes.data, validRedirect)
      if (platformLanding) {
        router.push(platformLanding, { scroll: false })
        return
      }

      const tenantLanding = resolveTenantLanding(userInfoRes.data)
      if (tenantLanding) {
        router.push(tenantLanding, { scroll: false })
        return
      }

      if (redirectTo && isValidRedirectPath(redirectTo)) {
        router.push(redirectTo, { scroll: false })
      } else if (userInfoRes.data.roles.find((role) => role.name === 'Admin')) {
        router.push(`/admin`, { scroll: false })
      } else {
        router.push(`/`, { scroll: false })
      }
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
      text: t('page.verifyEmail.resendSuccess'),
    })
    setCountdown(15)
  }

  return (
    <Formik initialValues={{ username: '', password: '', tenantIdentifier: '' }} validationSchema={validationSchema} onSubmit={submitForm}>
      {() => (
        <Form className="space-y-5 dark:text-white">
          <FormInput label={t('form.label.username')} name="username" placeholder={t('form.placeholder.username')} icon={<Mail size={16} />} autoFocus={true} required={true} />
          <FormPasswordInput label={t('form.label.password')} name="password" placeholder={t('form.placeholder.password')} icon={<Lock size={16} />} required={true} />
          <FormInput label={t('form.label.tenant')} name="tenantIdentifier" placeholder={t('form.placeholder.tenant')} icon={<Building2 size={16} />} />

          {showResendLink && (
            <div role="alert" className="relative flex items-start gap-3 rounded-lg border border-danger/30 bg-danger-light p-4 text-sm dark:border-danger/40 dark:bg-danger/10">
              <AlertCircle className="mt-0.5 h-5 w-5 shrink-0 text-danger" />
              <div className="flex-1 space-y-2">
                <p className="text-danger-dark font-semibold dark:text-danger">{t('page.verifyEmail.notVerifiedTitle')}</p>
                <p className="text-danger-dark/80 dark:text-danger/80">{t('page.verifyEmail.notVerifiedMessage')}</p>
                <Button type="button" className="btn btn-outline-primary" onClick={handleResendEmail} disabled={countdown > 0 || isResending} isLoading={isResending}>
                  {countdown > 0 ? t('page.verifyEmail.resendWait', { seconds: countdown }) : t('page.verifyEmail.resendButton')}
                </Button>
              </div>
            </div>
          )}

          <div className="flex items-center justify-between">
            <div className="flex gap-2">
              <span className="text-sm dark:text-gray-400">{t('page.auth.signin.noAccount')}</span>
              <LocalizedLink href="/signup" className="text-sm text-primary hover:underline dark:text-white">
                {t('page.auth.signin.signupLink')}
              </LocalizedLink>
            </div>
            <LocalizedLink href="/forget-password" className="text-sm text-primary hover:underline dark:text-white">
              {t('page.auth.signin.forgotPassword')}
            </LocalizedLink>
          </div>

          <Button type="submit" className="btn w-full border-0 btn-gradient uppercase shadow-[0_10px_20px_-10px_rgba(67,97,238,0.44)]" isLoading={isTokenLoading || isLoadingUserInfo}>
            {t('page.auth.signin.button')}
          </Button>
        </Form>
      )}
    </Formik>
  )
}
