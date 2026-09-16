import { Metadata } from 'next'
import { getServerTranslation } from '@/i18n'
import { NoTenantView } from './_components/no-tenant-view'

export async function generateMetadata({ params }: { params: Promise<{ lang: string }> }): Promise<Metadata> {
  const { lang } = await params
  return {
    title: await getServerTranslation(lang, 'page.noTenant.title'),
  }
}

/**
 * Server-rendered no-tenant route that renders the screen shown to an authenticated user who holds no active membership in any tenant.
 */
const NoTenantPage = () => {
  return <NoTenantView />
}

export default NoTenantPage
