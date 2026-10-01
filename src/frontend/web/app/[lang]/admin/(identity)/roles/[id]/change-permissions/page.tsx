import { getServerTranslation } from '@/i18n'
import { ChangePermissionsForm } from './_components/change-permissions-form'
import { AdminPageContent } from '@/components/layouts'

/**
 * Props for the role change-permissions page, providing the route lang segment and the target role id.
 */
interface ChangePermissionsPageProps {
  params: Promise<{
    lang: string
    id: string
  }>
}

/**
 * Server-rendered role change-permissions page that loads the localized title and delegates to the interactive change-permissions form for the specified role id.
 */
const ChangePermissions = async ({ params }: ChangePermissionsPageProps) => {
  const { lang, id } = await params
  const title = await getServerTranslation(lang, 'page.roles.changePermissions.title')

  return (
    <AdminPageContent
      title={title}
      innerClassName='max-w-175'
    >
      <ChangePermissionsForm roleId={id} />
    </AdminPageContent>
  )
}

export default ChangePermissions
