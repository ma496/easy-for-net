'use client'

import { useVerifyEmailMutation } from '@/store/api/identity/account/account-api'
import { useSearchParams } from 'next/navigation'
import { useEffect, useState, useRef } from 'react'
import { useTranslation } from '@/i18n'
import { LocalizedLink } from '@/components/ui/localized-link'
import { Loader2, CheckCircle, XCircle } from 'lucide-react'

/**
 * Interactive client-side view that handles the email-verification callback by reading the token from the URL and calling the verify API.
 * Displays a verifying spinner, success, or error state with appropriate navigation back to the sign-in page.
 */
export const VerifyEmailView = () => {
  const searchParams = useSearchParams()
  const token = searchParams.get('token')
  const { t } = useTranslation()


  const [verifyEmail] = useVerifyEmailMutation()
  const [status, setStatus] = useState<'verifying' | 'success' | 'error'>('verifying')
  const effectRan = useRef(false)

  useEffect(() => {
    if (effectRan.current) return

    if (token) {
      effectRan.current = true
      verifyEmail({ token })
        .unwrap()
        .then(() => setStatus('success'))
        .catch(() => setStatus('error'))
    } else {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setStatus('error')
    }
  }, [token, verifyEmail])

  return (
    <div className="flex flex-col items-center justify-center space-y-6 text-center dark:text-white">
      {status === 'verifying' && (
        <>
          <Loader2 className="animate-spin h-12 w-12 text-primary" />
          <h2 className="text-xl font-semibold">{t('page.verifyEmail.verifying') || 'Verifying your email...'}</h2>
        </>
      )}

      {status === 'success' && (
        <>
          <CheckCircle className="h-16 w-16 text-green-500" />
          <h2 className="text-2xl font-bold">{t('page.verifyEmail.verifiedTitle') || 'Email Verified!'}</h2>
          <p>{t('page.verifyEmail.verifiedMessage') || 'Your email has been successfully verified. You can now sign in.'}</p>
          <LocalizedLink href="/signin" className="btn btn-primary mt-4">
            {t('page.verifyEmail.buttonSignin') || 'Sign In Now'}
          </LocalizedLink>
        </>
      )}

      {status === 'error' && (
        <>
          <XCircle className="h-16 w-16 text-red-500" />
          <h2 className="text-2xl font-bold">{t('page.verifyEmail.failedTitle') || 'Verification Failed'}</h2>
          <p>{t('page.verifyEmail.failedMessage') || 'The verification link is invalid or has expired.'}</p>
          <LocalizedLink href="/signin" className="btn btn-outline-primary mt-4">
            {t('page.auth.signin.backToSignin') || 'Back to Sign In'}
          </LocalizedLink>
        </>
      )}
    </div>
  )
}

