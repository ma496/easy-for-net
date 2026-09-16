import { getServerTranslation } from '@/i18n'
import { TenantMemberTable } from './_components/tenant-member-table'
import { AdminPageContent } from '@/components/layouts'

/**
 * Props for the tenant members page, providing the route lang segment and the id of the tenant whose members are managed.
 */
interface TenantMembersPageProps {
  params: Promise<{
    lang: string
    id: string
  }>
}

/**
 * Server-rendered tenant members page that resolves the localized title and renders the interactive member table for the specified tenant id.
 */
const TenantMembers = async ({ params }: TenantMembersPageProps) => {
  const { lang, id } = await params
  const title = await getServerTranslation(lang, 'page.tenants.members.title')

  return (
    <AdminPageContent title={title}>
      <TenantMemberTable tenantId={id} />
    </AdminPageContent>
  )
}

export default TenantMembers
