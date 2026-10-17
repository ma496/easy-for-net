'use client'

import * as Yup from 'yup'
import { useTranslation } from '@/i18n'
import { Formik, Form, FormikHelpers } from 'formik'
import { FormInput, FormPasswordInput } from '@/components/ui/form'
import { useSignupMutation, useResendVerifyEmailMutation } from '@/store/api/identity'
import { Button, LocalizedLink } from '@/components/ui'
import { NODE_MARK, withNode } from '@/components/custom'
import { useLocalizedRouter } from '@/hooks'
import { useState, useEffect, ReactNode, ChangeEvent } from 'react'
import { Mail, Lock, CheckCircle, Building2, Globe } from 'lucide-react'
import { apiErrorAlert, getErrorCode, pathOnOrigin, successToast, tenantUrlFromTemplate, tenantSigninUrl } from '@/lib/utils'
import type { HostTenant } from '@/lib/server/host-tenant'

/** The shape the API accepts for a tenant identifier, which is also its address's first host label. */
const tenantIdentifierPattern = /^[a-z0-9]+(?:-[a-z0-9]+)*$/

/** The id of the identifier input, so a reserved-name refusal can put the caret back in it. */
const tenantIdentifierInputId = 'signup-tenant-identifier'

/** Props for SignupForm: what the visitor's host names, fetched on the server by the page. */
interface SignupFormProps {
  hostTenant: HostTenant
}

/** What the success view needs once the tenant exists. */
interface SignupResult {
  message: string
  isVerificationRequired: boolean
  identifier: string
}

/** Props for the centred block that replaces the form when sign-up does not happen on this address. */
interface SignupNoticeProps {
  title: string
  message: ReactNode
  children?: ReactNode
}

/** A centred icon tile, heading and message in place of the form, with the ways on beneath it. */
const SignupNotice = ({ title, message, children }: SignupNoticeProps) => (
  <div className="flex flex-col items-center text-center">
    <div className="mb-5 flex size-12 items-center justify-center rounded-xl bg-surface-2 text-muted-foreground">
      <Building2 className="size-6" />
    </div>
    <h1 className="text-2xl font-semibold tracking-tight wrap-break-word text-foreground">{title}</h1>
    <p className="mt-2 text-sm wrap-break-word text-muted-foreground">{message}</p>
    <div className="mt-6 flex w-full flex-col gap-2">{children}</div>
  </div>
)

/** Props for the live preview of the address the identifier gives the new tenant. */
interface AddressPreviewProps {
  template: string
  identifier: string
}

/**
 * The address the new tenant will answer at, built live from the API's `tenantUrlTemplate`: the
 * template with a placeholder name while the field is empty, the typed identifier once it has the shape
 * the API accepts, and nothing while it has not (the validation error takes the slot instead).
 */
const AddressPreview = ({ template, identifier }: AddressPreviewProps) => {
  const { t } = useTranslation()
  const normalized = identifier.trim().toLowerCase()
  const label = t('page.auth.signup.addressPreview') || 'Your tenant address:'

  if (!normalized) {
    const [before, after] = template.split('{tenant}')
    return (
      <>
        {label}{' '}
        <bdi dir="ltr" className="font-mono break-all">
          {before}
          <span className="text-subtle-foreground">{t('page.auth.signup.addressPlaceholder') || 'your-name'}</span>
          {after}
        </bdi>
      </>
    )
  }

  if (!tenantIdentifierPattern.test(normalized)) return null

  return (
    <>
      {label}{' '}
      <bdi dir="ltr" className="font-mono break-all text-foreground">
        {tenantUrlFromTemplate(template, normalized)}
      </bdi>
    </>
  )
}

/**
 * Interactive client-side form that registers a new user together with the tenant they will work in. Sign-up creates
 * a tenant, so it happens on the main site alone: on a tenant's own address the form gives way to a link to the main site's
 * sign-up. The identifier field previews the address it gives the new tenant, and a reserved name is refused on the field
 * itself. The success view links to the new tenant's own sign-in page, and offers a resend-verification-email countdown when
 * email verification is required.
 */
export const SignupForm = ({ hostTenant }: SignupFormProps) => {
  const { t } = useTranslation()
  const router = useLocalizedRouter()
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

  const host = hostTenant.kind === 'found' ? hostTenant.host : null
  const tenantHost = host?.kind === 'tenant' ? host : null
  const template = host?.tenantUrlTemplate ?? null
  const platformUrl = host?.platformUrl ?? (hostTenant.kind === 'notFound' ? hostTenant.platformUrl : null)

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
    // bounds mirror the API's own tenant rules, so the form refuses what the server would refuse -
    // except a reserved name, which only the API knows and which is shown on the field when refused.
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
      .matches(tenantIdentifierPattern, t('validation.tenantIdentifier') || 'Use lower-case letters, digits and single hyphens, starting and ending with a letter or digit'),
  })

  type SignupFormValues = Yup.InferType<typeof validationSchema>

  const submitForm = async (values: SignupFormValues, helpers: FormikHelpers<SignupFormValues>) => {
    const response = await signupApi(values)
    if (response.error) {
      if (getErrorCode(response.error) === 'tenantIdentifierReserved') {
        // Touched without validating, so the client rules (which accept the name) do not clear the
        // server's refusal straight away.
        await helpers.setFieldTouched('tenantIdentifier', true, false)
        helpers.setFieldError('tenantIdentifier', t('error.server.tenantIdentifierReserved') || 'This identifier is reserved. Choose another one.')
        document.getElementById(tenantIdentifierInputId)?.focus()
        return
      }
      apiErrorAlert(response.error)
      return
    }

    setRegisteredEmail(values.email)
    const isVerificationRequired = !!response.data?.isEmailVerificationRequired
    setResult({
      isVerificationRequired,
      identifier: values.tenantIdentifier.trim().toLowerCase(),
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

  if (tenantHost || hostTenant.kind === 'notFound') {
    // Sign-up creates a tenant, which the API accepts on the main site alone - so on a tenant's
    // address (or one that names no tenant) the way on is the main site's own sign-up.
    const signupUrl = platformUrl ? pathOnOrigin(platformUrl, router.localize('/signup')) : null

    return (
      <SignupNotice
        title={t('page.auth.signup.tenantHostTitle') || 'Sign-up happens on the main site'}
        message={
          tenantHost
            ? withNode(
                t('page.auth.signup.tenantHostMessage', { name: NODE_MARK }) || `You're on ${NODE_MARK}. To create a tenant of your own, sign up on the main site.`,
                <bdi className="font-medium text-foreground">{tenantHost.name}</bdi>,
              )
            : t('page.auth.signup.mainSiteMessage') || 'To create a tenant of your own, sign up on the main site.'
        }
      >
        {signupUrl ? (
          <a className="btn btn-primary w-full" href={signupUrl}>
            {t('page.auth.signup.tenantHostAction') || 'Go to sign-up'}
          </a>
        ) : (
          // Without the platform's address there is no main site to point at; this origin's own home page still answers.
          <LocalizedLink href="/" className="btn btn-primary w-full">
            {t('page.auth.tenantNotFound.homeAction') || 'Go to home page'}
          </LocalizedLink>
        )}
        {tenantHost && (
          <LocalizedLink href="/signin" className="btn btn-secondary w-full">
            {t('page.auth.signin.backToSignin') || 'Back to Sign In'}
          </LocalizedLink>
        )}
      </SignupNotice>
    )
  }

  if (result) {
    const tenantAddress = tenantUrlFromTemplate(template, result.identifier)
    const signinPath = router.localize('/signin')
    const signinUrl = platformUrl ? tenantSigninUrl({ tenantUrlTemplate: template, platformUrl, identifier: result.identifier }, signinPath) : null

    return (
      <div className="flex flex-col items-center text-center">
        <div className="mb-5 flex size-12 items-center justify-center rounded-xl bg-success/10 text-success">
          <CheckCircle className="size-6" />
        </div>
        <h1 className="text-2xl font-semibold tracking-tight wrap-break-word text-foreground">
          {result.isVerificationRequired ? t('page.auth.signup.successTitle') || 'Success!' : t('page.auth.signup.readyTitle') || 'Your tenant is ready'}
        </h1>
        <p className="mt-2 text-sm text-muted-foreground">{result.message}</p>
        {tenantAddress && (
          <div className="mt-4 w-full rounded-lg bg-surface-2 px-3 py-2">
            <bdi dir="ltr" className="font-mono text-sm break-all text-foreground">
              {tenantAddress}
            </bdi>
          </div>
        )}
        <div className="mt-6 flex w-full flex-col gap-2">
          {result.isVerificationRequired && (
            <Button type="button" variant="outline" className="w-full" onClick={handleResendEmail} disabled={countdown > 0 || isResendingVerifyEmail} isLoading={isResendingVerifyEmail}>
              {countdown > 0 ? t('page.verifyEmail.resendWait', { seconds: countdown }) || `Please wait ${countdown}s` : t('page.verifyEmail.resendButton') || 'Resend Verification Email'}
            </Button>
          )}

          {signinUrl ? (
            <a className="btn btn-primary w-full" href={signinUrl}>
              {tenantAddress ? t('page.auth.signup.goToTenant') || 'Go to your tenant' : t('page.auth.signin.backToSignin') || 'Back to Sign In'}
            </a>
          ) : (
            <LocalizedLink href="/signin" className="btn btn-primary w-full">
              {t('page.auth.signin.backToSignin') || 'Back to Sign In'}
            </LocalizedLink>
          )}
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
      <Formik initialValues={{ username: '', email: '', password: '', confirmPassword: '', tenantName: '', tenantIdentifier: '' }} validationSchema={validationSchema} onSubmit={submitForm}>
        {({ values, setFieldValue }) => (
          <Form className="space-y-5">
            <FormInput
              label={t('form.label.username') || 'Username'}
              name="username"
              placeholder={t('form.placeholder.username') || 'Enter username'}
              icon={<Mail size={16} />}
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

            <div className="border-t border-border pt-5">
              <p className="mb-4 text-[11px] font-semibold tracking-wider text-subtle-foreground uppercase rtl:tracking-normal">{t('page.auth.signup.tenantSectionTitle') || 'Your tenant'}</p>
              <div className="space-y-5">
                <FormInput
                  label={t('page.auth.signup.tenantName') || 'Tenant name'}
                  name="tenantName"
                  placeholder={t('form.placeholder.tenantName') || 'Enter tenant name'}
                  icon={<Building2 size={16} />}
                  required={true}
                />
                <FormInput
                  id={tenantIdentifierInputId}
                  label={t('page.auth.signup.tenantAddress') || 'Tenant address'}
                  name="tenantIdentifier"
                  placeholder={t('form.placeholder.tenantIdentifier') || 'Enter identifier (for example acme)'}
                  icon={<Globe size={16} />}
                  required={true}
                  // An address is lower-case, so "Acme" is taken as `acme` while it is typed rather than refused.
                  onChange={(event: ChangeEvent<HTMLInputElement>) => setFieldValue('tenantIdentifier', event.target.value.toLowerCase())}
                  autoCapitalize="none"
                  spellCheck={false}
                  hint={template ? <AddressPreview template={template} identifier={values.tenantIdentifier} /> : t('page.auth.signup.identifierHint') || 'Lower-case letters, digits and hyphens.'}
                />
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
    </>
  )
}
