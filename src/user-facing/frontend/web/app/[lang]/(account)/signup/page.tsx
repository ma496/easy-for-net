import { SignupClosedNotice, SignupForm } from './_components/signup-form'
import { getIsSignupEnabled } from '@/lib/server/signup-status'
import { AuthShell } from '../_components/auth-shell'
import { Metadata } from 'next'
import { getServerTranslation } from '@/i18n'

export async function generateMetadata({ params }: { params: Promise<{ lang: string }> }): Promise<Metadata> {
  const { lang } = await params
  return {
    title: (await getServerTranslation(lang, 'page.auth.signup.title')) || 'Sign Up',
    robots: { index: false, follow: true },
  }
}

/**
 * Server-rendered sign-up route, framing the form in the split auth layout untitled: the form titles
 * each of its own states - the form, the success view and the "sign-up is closed" notice - so each has
 * exactly one main heading. Whether sign-up is open is asked on the server, so a closed site serves the
 * notice in place of the form rather than refusing it after the visitor has filled it in.
 */
const Signup = async ({ params }: { params: Promise<{ lang: string }> }) => {
  await params
  const isSignupEnabled = await getIsSignupEnabled()

  return <AuthShell>{isSignupEnabled ? <SignupForm /> : <SignupClosedNotice />}</AuthShell>
}

export default Signup
