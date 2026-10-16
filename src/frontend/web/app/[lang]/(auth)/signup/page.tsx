import { SignupForm } from './_components/signup-form'
import { AuthShell } from '../_components/auth-shell'
import { Metadata } from 'next'
import { getServerTranslation } from '@/i18n'
import { getHostWorkspace } from '@/lib/server/host-workspace'

export async function generateMetadata({ params }: { params: Promise<{ lang: string }> }): Promise<Metadata> {
  const { lang } = await params
  return {
    title: (await getServerTranslation(lang, 'page.auth.signup.title')) || 'Sign Up',
  }
}

/**
 * Server-rendered sign-up route under the auth route group.
 * Asks the API what the visitor's host names before rendering, and frames the form in the split auth layout untitled: the form
 * titles each of its own states - the form, the success view, and the link to the main site that replaces the form on a tenant's
 * address (or one naming no workspace) - so each has exactly one main heading.
 */
const BoxedSignup = async ({ params }: { params: Promise<{ lang: string }> }) => {
  await params
  const hostWorkspace = await getHostWorkspace()

  return (
    <AuthShell>
      <SignupForm hostWorkspace={hostWorkspace} />
    </AuthShell>
  )
}

export default BoxedSignup
