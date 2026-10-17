import { Metadata } from 'next'
import { getServerTranslation } from '@/i18n'
import { getHostTenant } from '@/lib/server/host-tenant'
import { AuthShell } from '../_components/auth-shell'
import { TicketSigninView } from './_components/ticket-signin-view'

export async function generateMetadata({ params }: { params: Promise<{ lang: string }> }): Promise<Metadata> {
  const { lang } = await params
  return {
    title: (await getServerTranslation(lang, 'page.auth.signin.title')) || 'Sign In',
  }
}

/**
 * Server-rendered route a tenant's address is opened on from another host of the same deployment. The
 * client view takes a single-use sign-in ticket from the page that opened it - accepted only from the
 * platform's address or a tenant's, both named by `GET /tenants/host` - and redeems it for a session on
 * this host.
 */
const TicketSignin = async ({ params }: { params: Promise<{ lang: string }> }) => {
  await params
  const hostTenant = await getHostTenant()
  const host = hostTenant.kind === 'found' ? hostTenant.host : null

  return (
    <AuthShell>
      <TicketSigninView platformUrl={host?.platformUrl ?? null} tenantUrlTemplate={host?.tenantUrlTemplate ?? null} />
    </AuthShell>
  )
}

export default TicketSignin
