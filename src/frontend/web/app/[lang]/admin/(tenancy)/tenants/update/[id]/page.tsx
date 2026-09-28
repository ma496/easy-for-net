import { getServerTranslation } from '@/i18n'
import { TenantUpdateForm } from './_components/tenant-update-form'
import { AdminPageContent } from '@/components/layouts'

/**
 * Props for the tenant update page, providing the route lang segment and the id of the tenant being edited.
 */
interface TenantUpdatePageProps {
  params: Promise<{
    lang: string
    id: string
  }>
}

/**
 * Server-rendered tenant update page that resolves the localized title and renders the interactive tenant-update form for the specified tenant id.
 */
const TenantUpdate = async ({ params }: TenantUpdatePageProps) => {
  const { lang, id } = await params
  const title = await getServerTranslation(lang, 'page.tenants.update.title')

  return (
    <AdminPageContent
      title={title}
      innerClassName='max-w-155'
    >
      <TenantUpdateForm tenantId={id} />
    </AdminPageContent>
  )
}

export default TenantUpdate
