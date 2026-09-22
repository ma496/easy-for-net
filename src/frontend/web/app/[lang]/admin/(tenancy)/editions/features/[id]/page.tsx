import { getServerTranslation } from '@/i18n'
import { FeatureValueEditor } from '../../../_components/feature-value-editor'
import { AdminPageContent } from '@/components/layouts'
import { FeatureValueProvider } from '@/store/api/tenancy'

/**
 * Props for the edition features page, providing the route lang segment and the id of the plan whose
 * entitlements are being edited.
 */
interface EditionFeaturesPageProps {
  params: Promise<{
    lang: string
    id: string
  }>
}

/**
 * Server-rendered page that edits what one plan is worth. Every tenant on the plan inherits these
 * values unless it overrides one for itself.
 */
const EditionFeatures = async ({ params }: EditionFeaturesPageProps) => {
  const { lang, id } = await params
  const title = await getServerTranslation(lang, 'page.features.editionTitle')

  return (
    <AdminPageContent title={title}>
      <FeatureValueEditor
        providerName={FeatureValueProvider.Edition}
        providerKey={id}
        returnUrl="/admin/editions/list"
      />
    </AdminPageContent>
  )
}

export default EditionFeatures
