import { getServerTranslation } from '@/i18n'
import { RoleUpdateForm } from './_components/role-update-form'
import { AdminPageContent } from '@/components/layouts'

/**
 * Props for the role update page, providing the route lang segment and the id of the role being edited.
 */
interface RoleUpdatePageProps {
  params: Promise<{
    lang: string
    id: string
  }>
}

/**
 * Server-rendered role update page that resolves the localized title and renders the interactive role-update form for the specified role id.
 */
const RoleUpdate = async ({ params }: RoleUpdatePageProps) => {
  const { lang, id } = await params
  const title = await getServerTranslation(lang, 'page.roles.update.title')

  return (
    <AdminPageContent
      title={title}
      innerClassName='max-w-155'
    >
      <RoleUpdateForm roleId={id} />
    </AdminPageContent>
  )
}

export default RoleUpdate
