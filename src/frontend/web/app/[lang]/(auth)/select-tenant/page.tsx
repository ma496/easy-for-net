import { Metadata } from 'next'
import { getServerTranslation } from '@/i18n'
import { SelectTenantView } from './_components/select-tenant-view'

export async function generateMetadata({ params }: { params: Promise<{ lang: string }> }): Promise<Metadata> {
  const { lang } = await params
  return {
    title: (await getServerTranslation(lang, 'page.selectTenant.title')) || 'Your tenants',
  }
}

/**
 * Server-rendered tenant selection route that renders the list of links to the tenants the signed-in account belongs to, each at its own address.
 */
const SelectTenantPage = () => {
  return <SelectTenantView />
}

export default SelectTenantPage
