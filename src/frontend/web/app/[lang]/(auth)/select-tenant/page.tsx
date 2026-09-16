import { Metadata } from 'next'
import { getServerTranslation } from '@/i18n'
import { SelectTenantView } from './_components/select-tenant-view'

export async function generateMetadata({ params }: { params: Promise<{ lang: string }> }): Promise<Metadata> {
  const { lang } = await params
  return {
    title: await getServerTranslation(lang, 'page.selectTenant.title'),
  }
}

/**
 * Server-rendered tenant selection route that renders the interactive chooser for a user holding an active membership in more than one tenant.
 */
const SelectTenantPage = () => {
  return <SelectTenantView />
}

export default SelectTenantPage
