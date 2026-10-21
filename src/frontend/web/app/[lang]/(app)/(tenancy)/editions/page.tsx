import { getServerTranslation } from '@/i18n'
import { EditionTable } from './_components/edition-table'
import { PageContent } from '@/components/layouts'

/**
 * Props for the editions list page, providing the localized route lang segment.
 */
interface EditionListPageProps {
  params: Promise<{ lang: string }>
}

/**
 * Server-rendered editions list page that resolves the localized title and renders the interactive
 * editions table within the app page shell.
 */
const EditionList = async ({ params }: EditionListPageProps) => {
  const { lang } = await params
  const title = (await getServerTranslation(lang, 'page.editions.title')) || 'Editions'

  return (
    <PageContent title={title}>
      <EditionTable />
    </PageContent>
  )
}

export default EditionList
