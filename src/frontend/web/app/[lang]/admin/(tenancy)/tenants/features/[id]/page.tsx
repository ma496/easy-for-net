import { getServerTranslation } from '@/i18n'
import { FeatureValueEditor } from '../../../_components/feature-value-editor'
import { AdminPageContent } from '@/components/layouts'
import { FeatureValueProvider } from '@/store/api/tenancy'

/**
 * Props for the tenant features page, providing the route lang segment and the id of the tenant whose
 * entitlements are being edited.
 */
interface TenantFeaturesPageProps {
  params: Promise<{
    lang: string
    id: string
  }>
}

/**
 * Server-rendered page that edits what one tenant is entitled to. A value set here overrides whatever
 * the tenant's plan grants; clearing it puts the plan back in charge.
 */
const TenantFeatures = async ({ params }: TenantFeaturesPageProps) => {
  const { lang, id } = await params
  const title = await getServerTranslation(lang, 'page.features.tenantTitle')

  return (
    <AdminPageContent title={title}>
      <FeatureValueEditor
        providerName={FeatureValueProvider.Tenant}
        providerKey={id}
        returnUrl="/admin/tenants/list"
      />
    </AdminPageContent>
  )
}

export default TenantFeatures
