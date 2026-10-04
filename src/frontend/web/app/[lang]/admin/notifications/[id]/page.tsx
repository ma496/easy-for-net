import { ArrowLeft } from 'lucide-react'
import { getServerTranslation } from '@/i18n'
import { NotificationDetail } from './_components/notification-detail'
import { AdminPageContent } from '@/components/layouts'
import { LocalizedLink } from '@/components/ui/localized-link'

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
  const title = await getServerTranslation(lang, 'page.notifications.title')
  const detailTitle = await getServerTranslation(lang, 'page.notifications.detail.title')
  const back = await getServerTranslation(lang, 'common.back')

  return (
    <AdminPageContent
      title={detailTitle}
      plain
      actions={
        <LocalizedLink href="/admin/notifications" className="btn btn-secondary btn-sm" aria-label={`${back}: ${title}`}>
          <ArrowLeft className="h-4 w-4 rtl:-scale-x-100" />
          {back}
        </LocalizedLink>
      }
    >
      <NotificationDetail id={id} />
    </AdminPageContent>
  )
}

export default NotificationDetailPage
