import { SigninForm } from './_components/signin-form'
import { getIsSignupEnabled } from '@/lib/server/signup-status'
import { AuthShell } from '../_components/auth-shell'
import { Metadata } from 'next'
import { getServerTranslation } from '@/i18n'

export async function generateMetadata({ params }: { params: Promise<{ lang: string }> }): Promise<Metadata> {
  const { lang } = await params
  return {
    title: (await getServerTranslation(lang, 'page.auth.signin.title')) || 'Sign In',
    robots: { index: false, follow: true },
  }
}

/**
 * Server-rendered sign-in route, framing the form in the split auth layout; the form links to sign-up
 * only while the site has it open.
 */
const SignIn = async ({ params }: { params: Promise<{ lang: string }> }) => {
  await params
  const isSignupEnabled = await getIsSignupEnabled()

  return (
    <AuthShell>
      <SigninForm isSignupEnabled={isSignupEnabled} />
    </AuthShell>
  )
}

export default SignIn
