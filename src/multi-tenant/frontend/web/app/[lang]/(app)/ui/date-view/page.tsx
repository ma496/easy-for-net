import { getServerTranslation } from '@/i18n'
import { DateViewExample } from './_components/date-view-example'
import { PageContent } from '@/components/layouts'

/**
 * Props for the date view showcase page, providing the localized route lang segment.
 */
interface DateViewPageProps {
  params: Promise<{ lang: string }>
}

/**
 * Server-rendered UI showcase page for the DateView read-only display component.
 * Resolves the localized title and renders the interactive date view example inside the app page shell.
 */
const DateViewPage = async ({ params }: DateViewPageProps) => {
  const { lang } = await params
  const title = (await getServerTranslation(lang, 'page.ui.dateView.title')) || 'UI Date View'

  return (
    <PageContent title={title} description="Dates displayed in one consistent, formatted way, including the empty and invalid cases." plain>
      <DateViewExample />
    </PageContent>
  )
}

export default DateViewPage
