import { getServerTranslation } from '@/i18n'
import { RoleTable } from './_components/role-table'
import { AdminPageContent } from '@/components/layouts'

/**
 * Props for the roles list page, providing the localized route lang segment.
 */
interface RolesProps {
  params: Promise<{ lang: string }>
}

/**
 * Server-rendered roles list page that resolves the localized title and renders the interactive role table inside the admin page shell.
 */
const Roles = async ({ params }: RolesProps) => {
  const { lang } = await params
  const title = await getServerTranslation(lang, 'page.roles.title')
  return (
    <AdminPageContent title={title}>
      <RoleTable />
    </AdminPageContent>
  )
}

export default Roles
