import { Metadata } from 'next'
import { ResetPasswordForm } from './_components/reset-password-form'
import { AuthShell } from '../_components/auth-shell'
import { getServerTranslation } from '@/i18n'

export async function generateMetadata({ params }: { params: Promise<{ lang: string }> }): Promise<Metadata> {
  const { lang } = await params
  return {
    title: await getServerTranslation(lang, 'page.auth.resetPassword.title'),
  }
}

/**
 * Server-rendered reset-password route under the auth route group.
 * Loads localized title/description, frames the form in the split auth layout, and renders the reset-password form (which uses a token from the URL).
 */
const ResetPassword = async ({ params }: { params: Promise<{ lang: string }> }) => {
  const { lang } = await params
  const [title, description] = await Promise.all([
    getServerTranslation(lang, 'page.auth.resetPassword.title'),
    getServerTranslation(lang, 'page.auth.resetPassword.description'),
  ])

  return (
    <AuthShell title={title} description={description}>
      <ResetPasswordForm />
    </AuthShell>
  )
}

export default ResetPassword
