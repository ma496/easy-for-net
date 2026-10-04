import { SigninForm } from './_components/signin-form'
import { AuthShell } from '../_components/auth-shell'
import { Metadata } from 'next'
import { getServerTranslation } from '@/i18n'

export async function generateMetadata({ params }: { params: Promise<{ lang: string }> }): Promise<Metadata> {
  const { lang } = await params
  return {
    title: await getServerTranslation(lang, 'page.auth.signin.title'),
  }
}

/**
 * Server-rendered sign-in route under the auth route group.
 * Loads the localized title/description, frames the sign-in form in the split auth layout, and renders the interactive form.
 */
const BoxedSignIn = async ({ params }: { params: Promise<{ lang: string }> }) => {
  const { lang } = await params
  const [title, description] = await Promise.all([
    getServerTranslation(lang, 'page.auth.signin.title'),
    getServerTranslation(lang, 'page.auth.signin.description'),
  ])

  return (
    <AuthShell title={title} description={description}>
      <SigninForm />
    </AuthShell>
  )
}

export default BoxedSignIn
