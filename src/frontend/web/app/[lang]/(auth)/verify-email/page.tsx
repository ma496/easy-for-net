import { getServerTranslation } from '@/i18n'
import { VerifyEmailView } from './_components/verify-email-view'
import { AuthShell } from '../_components/auth-shell'
import { Metadata } from 'next'

export async function generateMetadata({ params }: { params: Promise<{ lang: string }> }): Promise<Metadata> {
  const { lang } = await params
  return {
    title: await getServerTranslation(lang, 'page.verifyEmail.title'),
  }
}

/**
 * Server-routed wrapper for the email-verification page that frames the VerifyEmailView component with the split auth layout.
 */
const BoxedVerifyEmail = async () => {
  return (
    <AuthShell>
      <VerifyEmailView />
    </AuthShell>
  )
}

export default BoxedVerifyEmail
