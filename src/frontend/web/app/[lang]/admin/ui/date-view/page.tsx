import { getServerTranslation } from '@/i18n'
import { DateViewExample } from './_components/date-view-example'
import { AdminPageContent } from '@/components/layouts'

/**
 * Props for the date view showcase page, providing the localized route lang segment.
 */
interface DateViewPageProps {
  params: Promise<{ lang: string }>
}

/**
 * Server-rendered UI showcase page for the DateView read-only display component.
 * Resolves the localized title and renders the interactive date view example inside the admin page shell.
 */
const DateViewPage = async ({ params }: DateViewPageProps) => {
  const { lang } = await params
  const title = await getServerTranslation(lang, 'page.ui.dateView.title')

  return (
    <AdminPageContent title={title}>
      <DateViewExample />
    </AdminPageContent>
  )
}

export default DateViewPage
