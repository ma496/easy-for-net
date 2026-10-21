'use client'
import * as Yup from 'yup'
import { useTranslation } from '@/i18n'
import { Formik, Form } from 'formik'
import { FormInput, FormPasswordInput } from '@/components/ui/form'
import { User, Lock, AlertCircle, AlertTriangle, Building2 } from 'lucide-react'
import { useState, useEffect, ReactNode } from 'react'
import { useSearchParams } from 'next/navigation'
import { useTokenMutation, useLazyGetUserInfoQuery, useResendVerifyEmailMutation } from '@/store/api/identity'
import { TenantStatus } from '@/store/api/tenancy'
import { useAppDispatch } from '@/store/hooks'
import { setUserInfo } from '@/store/slices'
import { Button, LocalizedLink } from '@/components/ui'
import { NODE_MARK, withNode } from '@/components/custom'
import { useLocalizedRouter } from '@/hooks'
import { apiErrorAlert, getErrorCode, pathOnOrigin, resolvePlatformLanding, successToast } from '@/lib/utils'
import { isValidRedirectPath } from '@/lib/utils/redirect'
import type { HostTenant } from '@/lib/server/host-tenant'

/** Props for SigninForm: what the visitor's host names, fetched on the server by the page. */
interface SigninFormProps {
  hostTenant: HostTenant
}

/** Props for the centred block that replaces the form when there is nothing to sign in to here. */
interface SigninNoticeProps {
  title: string
  message: ReactNode
  action?: ReactNode
}

/** A centred icon tile, heading and message in place of the form, with one way on. */
const SigninNotice = ({ title, message, action }: SigninNoticeProps) => (
  <div className="flex flex-col items-center text-center">
    <div className="mb-5 flex size-12 items-center justify-center rounded-xl bg-surface-2 text-muted-foreground">
      <Building2 className="size-6" />
    </div>
    <h1 className="text-2xl font-semibold tracking-tight wrap-break-word text-foreground">{title}</h1>
    <p className="mt-2 text-sm wrap-break-word text-muted-foreground">{message}</p>
    {action}
  </div>
)

/**
 * Interactive client-side form that authenticates a user with username/password and routes them to the appropriate landing page.
 * The host decides the tenant: on a tenant's own address the heading names that tenant and a sign-in starts a session in it,
 * on the platform's address a platform account signs in with no tenant, and an ordinary account is told to use its tenant's
 * address instead. A host that names no tenant shows that, with a link to the main site, rather than a form that cannot succeed;
 * a suspended tenant keeps the form but cannot be signed in to. When the host could not be asked, the platform copy shows and
 * the form works as usual. Manages a verification-message sub-state with a resend-email countdown for accounts whose email is not
 * yet verified.
 */
export const SigninForm = ({ hostTenant }: SigninFormProps) => {
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
  const dispatch = useAppDispatch()

  const [showResendLink, setShowResendLink] = useState(false)
  const [registeredEmail, setRegisteredEmail] = useState('')
  const [countdown, setCountdown] = useState(0)
  const [isHostRequired, setIsHostRequired] = useState(false)

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

  const host = hostTenant.kind === 'found' ? hostTenant.host : null
  const tenantHost = host?.kind === 'tenant' ? host : null
  const isSuspended = tenantHost?.status === TenantStatus.Suspended
  const platformUrl = hostTenant.kind === 'found' ? hostTenant.host.platformUrl : hostTenant.kind === 'notFound' ? hostTenant.platformUrl : null
  const visitorHost = hostTenant.visitorHost ?? ''

  const submitForm = async (values: SigninFormValues) => {
    if (isSuspended) return

    const tokenRes = await tokenApi(values)
    if (tokenRes.error) {
      const code = getErrorCode(tokenRes.error)
      if (code === 'emailNotVerified') {
        setRegisteredEmail(values.username)
        setShowResendLink(true)
        return
      }
      if (code === 'tenantHostRequired') {
        // An ordinary account on the platform's address: its sign-in belongs on its tenant's own.
        setIsHostRequired(true)
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

      // The root layout persists across the client-side navigation below, so left alone it would
      // keep showing the texts and languages fetched for the anonymous visitor rather than the ones
      // this session's tenant (or platform) scope resolves to. A refresh makes it re-fetch them - but
      // only after the push below, on whichever branch actually lands: `router.push` schedules a
      // navigation and `router.refresh()` schedules a re-fetch of the current one, and Next drops a
      // refresh that was still pending when a navigation arrives, so calling it first is calling it on
      // the page just left rather than the one arrived at.

      // The host named the tenant, so an ordinary account arrives here already acting in it (or was
      // refused above). A platform account signing in on the platform's address acts in no tenant, and
      // its landing honours a `redirect` only when that screen answers in platform scope.
      const validRedirect = redirectTo && isValidRedirectPath(redirectTo) ? redirectTo : null
      const platformLanding = resolvePlatformLanding(userInfoRes.data, validRedirect)
      if (platformLanding) {
        router.push(platformLanding, { scroll: false })
        router.refresh()
        return
      }

      router.push(validRedirect ?? '/', { scroll: false })
      router.refresh()
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

  if (hostTenant.kind === 'notFound') {
    const hostNode = (
      <bdi dir="ltr" className="font-mono text-xs break-all text-foreground">
        {visitorHost}
      </bdi>
    )

    return (
      <SigninNotice
        title={t('page.auth.tenantNotFound.title') || 'Tenant not found'}
        message={
          visitorHost
            ? withNode(t('page.auth.tenantNotFound.message', { host: NODE_MARK }) || `There is no tenant at ${NODE_MARK}. Check the address, or go to the main site.`, hostNode)
            : t('page.auth.tenantNotFound.messageNoHost') || 'There is no tenant at this address. Check the address, or go to the main site.'
        }
        action={
          platformUrl ? (
            <a className="btn btn-primary mt-6 w-full" href={platformUrl}>
              {t('page.auth.tenantNotFound.action') || 'Go to the main site'}
            </a>
          ) : (
            // Without the platform's address there is no main site to point at; this origin's own home page still answers.
            <LocalizedLink href="/" className="btn btn-primary mt-6 w-full">
              {t('page.auth.tenantNotFound.homeAction') || 'Go to home page'}
            </LocalizedLink>
          )
        }
      />
    )
  }

  if (isHostRequired) {
    return (
      <SigninNotice
        title={t('page.auth.signin.hostRequiredTitle') || "Sign in at your tenant's address"}
        message={t('page.auth.signin.hostRequiredMessage') || "Your account belongs to a tenant. Open its address to sign in. If you don't know it, check your invitation email."}
        action={
          <Button type="button" size="lg" className="mt-6 w-full" onClick={() => setIsHostRequired(false)}>
            {t('page.auth.signin.backToSignin') || 'Back to Sign In'}
          </Button>
        }
      />
    )
  }

  return (
    <>
      <div className="mb-8">
        {tenantHost ? (
          <>
            <h1 className="text-2xl font-semibold tracking-tight wrap-break-word text-foreground">
              {withNode(t('page.auth.signin.titleTenant', { name: NODE_MARK }) || `Sign in to ${NODE_MARK}`, <bdi>{tenantHost.name}</bdi>)}
            </h1>
            <p className="mt-1.5 text-sm wrap-break-word text-muted-foreground">
              <bdi dir="ltr" className="font-mono text-xs break-all">
                {visitorHost || tenantHost.identifier}
              </bdi>
            </p>
          </>
        ) : (
          <>
            <h1 className="text-2xl font-semibold tracking-tight wrap-break-word text-foreground">{t('page.auth.signin.title') || 'Sign In'}</h1>
            <p className="mt-1.5 text-sm wrap-break-word text-muted-foreground">{t('page.auth.signin.description') || 'Enter your credentials to sign in'}</p>
          </>
        )}
      </div>

      <Formik initialValues={{ username: '', password: '' }} validationSchema={validationSchema} onSubmit={submitForm}>
        {() => (
          <Form className="space-y-5">
            {isSuspended && (
              <div role="alert" className="flex items-start gap-3 rounded-lg border border-warning/30 bg-warning/10 p-4 text-sm">
                <AlertTriangle className="mt-0.5 size-4 shrink-0 text-warning" />
                <div className="min-w-0 flex-1 space-y-1">
                  <p className="font-semibold text-foreground">{t('page.auth.signin.suspendedTitle') || 'This tenant is suspended'}</p>
                  <p className="text-foreground/80">{t('page.auth.signin.suspendedMessage') || 'Signing in is unavailable until an administrator reactivates it.'}</p>
                </div>
              </div>
            )}

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

            <Button type="submit" size="lg" className="w-full" isLoading={isTokenLoading || isLoadingUserInfo} disabled={isSuspended}>
              {t('page.auth.signin.button') || 'Sign In'}
            </Button>

            {tenantHost ? (
              // Sign-up creates a tenant, which happens on the main site alone.
              <p className="text-center text-sm text-muted-foreground">
                {t('page.auth.signin.ownTenant') || 'Want your own tenant?'}{' '}
                <a href={pathOnOrigin(tenantHost.platformUrl, router.localize('/signup'))} className="font-medium text-primary hover:underline">
                  {t('page.auth.signin.createTenant') || 'Create one'}
                </a>
              </p>
            ) : (
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
