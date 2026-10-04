'use client'

import { useVerifyEmailMutation } from '@/store/api/identity'
import { useSearchParams } from 'next/navigation'
import { useEffect, useState, useRef } from 'react'
import { useTranslation } from '@/i18n'
import { LocalizedLink } from '@/components/ui'
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
    <div className="flex flex-col items-center py-2 text-center" aria-live="polite">
      {status === 'verifying' && (
        <>
          <div className="mb-5 flex size-12 items-center justify-center rounded-xl bg-primary/10 text-primary">
            <Loader2 className="size-6 animate-spin" />
          </div>
          <h1 className="text-xl font-semibold tracking-tight">{t('page.verifyEmail.verifying')}</h1>
        </>
      )}

      {status === 'success' && (
        <>
          <div className="mb-5 flex size-12 items-center justify-center rounded-xl bg-success/10 text-success">
            <CheckCircle className="size-6" />
          </div>
          <h1 className="text-xl font-semibold tracking-tight">{t('page.verifyEmail.verifiedTitle')}</h1>
          <p className="mt-2 text-sm text-muted-foreground">{t('page.verifyEmail.verifiedMessage')}</p>
          <LocalizedLink href="/signin" className="btn btn-primary btn-lg mt-6 w-full">
            {t('page.verifyEmail.buttonSignin')}
          </LocalizedLink>
        </>
      )}

      {status === 'error' && (
        <>
          <div className="mb-5 flex size-12 items-center justify-center rounded-xl bg-danger/10 text-danger">
            <XCircle className="size-6" />
          </div>
          <h1 className="text-xl font-semibold tracking-tight">{t('page.verifyEmail.failedTitle')}</h1>
          <p className="mt-2 text-sm text-muted-foreground">{t('page.verifyEmail.failedMessage')}</p>
          <LocalizedLink href="/signin" className="btn btn-secondary btn-lg mt-6 w-full">
            {t('page.auth.signin.backToSignin')}
          </LocalizedLink>
        </>
      )}
    </div>
  )
}

