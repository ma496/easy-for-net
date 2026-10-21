import { getServerTranslation } from '@/i18n'
import { RoleCreateForm } from './_components/role-create-form'
import { PageContent } from '@/components/layouts'

/**
 * Props for the role create page, providing the localized route lang segment.
 */
interface RoleCreatePageProps {
  params: Promise<{ lang: string }>
}

/**
 * Server-rendered role create page that resolves the localized title and renders the interactive role-create form within the app page shell.
 */
const RoleCreate = async ({ params }: RoleCreatePageProps) => {
  const { lang } = await params
  const title = (await getServerTranslation(lang, 'page.roles.create.title')) || 'Create Role'

  return (
    <PageContent
      title={title}
      innerClassName='max-w-3xl'
    >
      <RoleCreateForm />
    </PageContent>
  )
}

export default RoleCreate
