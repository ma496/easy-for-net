import { getServerTranslation } from '@/i18n'
import { RoleCreateForm } from './_components/role-create-form'
import { AdminPageContent } from '@/components/layouts'

/**
 * Props for the role create page, providing the localized route lang segment.
 */
interface RoleCreatePageProps {
  params: Promise<{ lang: string }>
}

/**
 * Server-rendered role create page that resolves the localized title and renders the interactive role-create form within the admin page shell.
 */
const RoleCreate = async ({ params }: RoleCreatePageProps) => {
  const { lang } = await params
  const title = await getServerTranslation(lang, 'page.roles.create.title')

  return (
    <AdminPageContent
      title={title}
      innerClassName='max-w-155'
    >
      <RoleCreateForm />
    </AdminPageContent>
  )
}

export default RoleCreate
