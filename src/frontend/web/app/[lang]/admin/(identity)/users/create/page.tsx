import { getServerTranslation } from '@/i18n'
import { UserCreateForm } from './_components/user-create-form'
import { AdminPageContent } from '@/components/layouts'

/**
 * Props for the user create page, providing the localized route lang segment.
 */
interface UserCreatePageProps {
  params: Promise<{ lang: string }>
}

/**
 * Server-rendered user create page that resolves the localized title and renders the interactive user-create form within the admin page shell.
 */
const UserCreate = async ({ params }: UserCreatePageProps) => {
  const { lang } = await params
  const title = await getServerTranslation(lang, 'page.users.create.title')

  return (
    <AdminPageContent
      title={title}
      innerClassName='max-w-187.5'
    >
      <UserCreateForm />
    </AdminPageContent>
  )
}

export default UserCreate
