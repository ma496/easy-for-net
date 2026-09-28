import { getServerTranslation } from '@/i18n'
import { TenantCreateForm } from './_components/tenant-create-form'
import { AdminPageContent } from '@/components/layouts'

/**
 * Props for the tenant create page, providing the localized route lang segment.
 */
interface TenantCreatePageProps {
  params: Promise<{ lang: string }>
}

/**
 * Server-rendered tenant create page that resolves the localized title and renders the interactive tenant-create form within the admin page shell.
 */
const TenantCreate = async ({ params }: TenantCreatePageProps) => {
  const { lang } = await params
  const title = await getServerTranslation(lang, 'page.tenants.create.title')

  return (
    <AdminPageContent
      title={title}
      innerClassName='max-w-155'
    >
      <TenantCreateForm />
    </AdminPageContent>
  )
}

export default TenantCreate
