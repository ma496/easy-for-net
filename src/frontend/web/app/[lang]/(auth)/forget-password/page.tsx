import { Metadata } from 'next'
import { ForgetPasswordForm } from './_components/forget-password-form'
import { AuthShell } from '../_components/auth-shell'
import { getServerTranslation } from '@/i18n'

export async function generateMetadata({ params }: { params: Promise<{ lang: string }> }): Promise<Metadata> {
  const { lang } = await params
  return {
    title: (await getServerTranslation(lang, 'page.auth.forgotPassword.title')) || 'Forget Password',
  }
}

/**
 * Server-rendered forget-password route under the auth route group.
 * Resolves the localized title/description, frames the form in the split auth layout with a back link, and renders the request-password-reset form.
 */
const ForgetPassword = async ({ params }: { params: Promise<{ lang: string }> }) => {
  const { lang } = await params
  const [title, description] = await Promise.all([
    getServerTranslation(lang, 'page.auth.forgotPassword.title').then((text) => text || 'Forget Password'),
    getServerTranslation(lang, 'page.auth.forgotPassword.description').then((text) => text || 'Enter your email to reset your password'),
  ])

  return (
    <AuthShell title={title} description={description} showBack={true}>
      <ForgetPasswordForm />
    </AuthShell>
  )
}

export default ForgetPassword
