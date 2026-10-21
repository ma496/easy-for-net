import { SigninForm } from './_components/signin-form'
import { AuthShell } from '../_components/auth-shell'
import { Metadata } from 'next'
import { getServerTranslation } from '@/i18n'
import { getHostTenant } from '@/lib/server/host-tenant'

export async function generateMetadata({ params }: { params: Promise<{ lang: string }> }): Promise<Metadata> {
  const { lang } = await params
  return {
    title: (await getServerTranslation(lang, 'page.auth.signin.title')) || 'Sign In',
  }
}

/**
 * Server-rendered sign-in route under the auth route group.
 * Asks the API what the visitor's host names before rendering, so the form's own heading - the tenant's name on a tenant's
 * address, the platform copy otherwise, or a "tenant not found" block - is right on the first paint, and frames the form in
 * the split auth layout.
 */
const BoxedSignIn = async ({ params }: { params: Promise<{ lang: string }> }) => {
  await params
  const hostTenant = await getHostTenant()

  return (
    <AuthShell>
      <SigninForm hostTenant={hostTenant} />
    </AuthShell>
  )
}

export default BoxedSignIn
