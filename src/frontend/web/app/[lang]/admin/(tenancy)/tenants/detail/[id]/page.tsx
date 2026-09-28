import { getServerTranslation } from '@/i18n'
import { TenantDetailView } from './_components/tenant-detail-view'
import { AdminPageContent } from '@/components/layouts'

/**
 * Props for the tenant detail page, providing the route lang segment and the id of the tenant being shown.
 */
interface TenantDetailPageProps {
  params: Promise<{
    lang: string
    id: string
  }>
}

/**
 * Server-rendered tenant detail page that resolves the localized title and renders what one tenant is - its
 * identity, its lifecycle state and its members - for the specified tenant id.
 */
const TenantDetail = async ({ params }: TenantDetailPageProps) => {
  const { lang, id } = await params
  const title = await getServerTranslation(lang, 'page.tenants.detail.title')

  return (
    <AdminPageContent title={title}>
      <TenantDetailView tenantId={id} />
    </AdminPageContent>
  )
}

export default TenantDetail
