import { SignupForm } from './_components/signup-form'
import { AuthShell } from '../_components/auth-shell'
import { Metadata } from 'next'
import { getServerTranslation } from '@/i18n'

export async function generateMetadata({ params }: { params: Promise<{ lang: string }> }): Promise<Metadata> {
  const { lang } = await params
  return {
    title: (await getServerTranslation(lang, 'page.auth.signup.title')) || 'Sign Up',
  }
}

/**
 * Server-rendered sign-up route under the auth route group.
 * Loads the localized title/description, frames the sign-up form in the split auth layout, and renders the interactive form.
 */
const BoxedSignup = async ({ params }: { params: Promise<{ lang: string }> }) => {
  const { lang } = await params
  const [title, description] = await Promise.all([
    getServerTranslation(lang, 'page.auth.signup.title').then((text) => text || 'Sign Up'),
    getServerTranslation(lang, 'page.auth.signup.description').then((text) => text || 'Enter your email and password to register'),
  ])

  return (
    <AuthShell title={title} description={description}>
      <SignupForm />
    </AuthShell>
  )
}

export default BoxedSignup
