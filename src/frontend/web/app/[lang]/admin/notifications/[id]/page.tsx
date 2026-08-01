import { getServerTranslation } from '@/i18n'
import { NotificationDetail } from './_components/notification-detail'
import { AdminPageContent } from '@/components/layouts'

/**
 * Props for the notification detail page, providing the route lang segment and the target notification id.
 */
interface NotificationDetailPageProps {
  params: Promise<{ lang: string; id: string }>
}

/**
 * Server-rendered notification detail page that resolves the localized title and renders the interactive detail view for the specified notification id.
 */
const NotificationDetailPage = async ({ params }: NotificationDetailPageProps) => {
  const { lang, id } = await params
  const title = await getServerTranslation(lang, 'page.notifications.detail.title')

  return (
    <AdminPageContent
      title={title}
    >
      <NotificationDetail id={id} />
    </AdminPageContent>
  )
}

export default NotificationDetailPage
