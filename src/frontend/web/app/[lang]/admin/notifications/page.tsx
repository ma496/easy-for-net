import { getServerTranslation } from '@/i18n'
import { NotificationTable } from './_components/notification-table'
import { AdminPageContent } from '@/components/layouts'

/**
 * Props for the notifications list page, providing the localized route lang segment.
 */
interface NotificationsProps {
  params: Promise<{ lang: string }>
}

/**
 * Server-rendered notifications list page that resolves the localized title and renders the interactive notification table inside the admin page shell.
 */
const Notifications = async ({ params }: NotificationsProps) => {
  const { lang } = await params
  const title = await getServerTranslation(lang, 'page.notifications.title')
  return (
    <AdminPageContent title={title}>
      <NotificationTable />
    </AdminPageContent>
  )
}

export default Notifications
