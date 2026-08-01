'use client'
import * as Yup from 'yup'
import { useTranslation } from '@/i18n'
import { Formik, Form } from 'formik'
import { FormInput } from '@/components/ui/form/form-input'
import { Mail, Lock, AlertCircle } from 'lucide-react'
import { useState, useEffect } from 'react'
import { useTokenMutation, useLazyGetUserInfoQuery, useResendVerifyEmailMutation } from '@/store/api/identity/account/account-api'
import { useAppDispatch } from '@/store/hooks'
import { setUserInfo } from '@/store/slices/authSlice'
import { Button } from '@/components/ui/button'
import { FormPasswordInput } from '@/components/ui/form/form-password-input'
import { LocalizedLink } from '@/components/ui/localized-link'
import { successToast } from '@/lib/utils/notification'
import { useLocalizedRouter } from '@/hooks/use-localized-router'
import { apiErrorAlert } from '@/lib/utils'

/**
 * Interactive client-side form that authenticates a user with username/password and routes them to the appropriate landing page.
 * Manages a verification-message sub-state with a resend-email countdown for accounts whose email is not yet verified.
 */
export const SigninForm = () => {
  const router = useLocalizedRouter()
  const { t } = useTranslation()

  const validationSchema = Yup.object().shape({
    username: Yup.string()
      .required(t('validation.required'))
      .min(3, t('validation.minLength', { min: 3 }))
      .max(50, t('validation.maxLength', { max: 50 })),
    password: Yup.string()
      .required(t('validation.required'))
      .min(8, t('validation.minLength', { min: 8 }))
      .max(50, t('validation.maxLength', { max: 50 })),
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
    const tokenRes = await tokenApi(values)
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
      if (userInfoRes.data.roles.find((role) => role.name === 'Public')) {
        router.push(`/`, { scroll: false })
      } else {
        router.push(`/admin`, { scroll: false })
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
    <Formik initialValues={{ username: '', password: '' }} validationSchema={validationSchema} onSubmit={submitForm}>
      {() => (
        <Form className="space-y-5 dark:text-white">
          <FormInput label={t('form.label.username')} name="username" placeholder={t('form.placeholder.username')} icon={<Mail size={16} />} autoFocus={true} required={true} />
          <FormPasswordInput label={t('form.label.password')} name="password" placeholder={t('form.placeholder.password')} icon={<Lock size={16} />} required={true} />

          {showResendLink && (
            <div role="alert" className="relative flex items-start gap-3 rounded-lg border border-danger/30 bg-danger-light p-4 text-sm dark:border-danger/40 dark:bg-danger/10">
              <AlertCircle className="mt-0.5 h-5 w-5 shrink-0 text-danger" />
              <div className="flex-1 space-y-2">
                <p className="font-semibold text-danger-dark dark:text-danger">{t('page.verifyEmail.notVerifiedTitle')}</p>
                <p className="text-danger-dark/80 dark:text-danger/80">{t('page.verifyEmail.notVerifiedMessage')}</p>
                <Button
                  type="button"
                  className="btn btn-outline-primary"
                  onClick={handleResendEmail}
                  disabled={countdown > 0 || isResending}
                  isLoading={isResending}
                >
                  {countdown > 0 ? t('page.verifyEmail.resendWait', { seconds: countdown }) : t('page.verifyEmail.resendButton')}
                </Button>
              </div>
            </div>
          )}

          <div className="flex justify-between items-center">
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

