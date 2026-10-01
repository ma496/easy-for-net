import { getServerTranslation } from '@/i18n'
import { UserUpdateForm } from './_components/user-update-form'
import { AdminPageContent } from '@/components/layouts'

/**
 * Props for the user update page, providing the route lang segment and the id of the user being edited.
 */
interface UserUpdatePageProps {
  params: Promise<{
    lang: string
    id: string
  }>
}

/**
 * Server-rendered user update page that resolves the localized title and renders the interactive user-update form for the specified user id.
 */
const UserUpdate = async ({ params }: UserUpdatePageProps) => {
  const { lang, id } = await params
  const title = await getServerTranslation(lang, 'page.users.update.title')

  return (
    <AdminPageContent
      title={title}
      innerClassName='max-w-155'
    >
      <UserUpdateForm userId={id} />
    </AdminPageContent>
  )
}

export default UserUpdate
