import { getServerTranslation } from '@/i18n'
import { TenantTable } from './_components/tenant-table'
import { AdminPageContent } from '@/components/layouts'

/**
 * Props for the tenants list page, providing the localized route lang segment.
 */
interface TenantsProps {
  params: Promise<{ lang: string }>
}

/**
 * Server-rendered tenants list page that resolves the localized title and renders the interactive tenant table inside the admin page shell.
 */
const Tenants = async ({ params }: TenantsProps) => {
  const { lang } = await params
  const title = await getServerTranslation(lang, 'page.tenants.list.title')
  return (
    <AdminPageContent title={title}>
      <TenantTable />
    </AdminPageContent>
  )
}

export default Tenants
