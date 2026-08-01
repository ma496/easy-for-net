import { getServerTranslation } from '@/i18n'
import { UserTable } from './_components/user-table'
import { AdminPageContent } from '@/components/layouts'

/**
 * Props for the users list page, providing the localized route lang segment.
 */
interface UsersProps {
  params: Promise<{ lang: string }>
}

/**
 * Server-rendered users list page that resolves the localized title and renders the interactive user table inside the admin page shell.
 */
const Users = async ({ params }: UsersProps) => {
  const { lang } = await params
  const title = await getServerTranslation(lang, 'page.users.title')
  return (
    <AdminPageContent title={title}>
      <UserTable />
    </AdminPageContent>
  )
}

export default Users
