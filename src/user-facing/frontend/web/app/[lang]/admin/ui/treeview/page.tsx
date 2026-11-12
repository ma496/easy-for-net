import { getServerTranslation } from '@/i18n'
import { TreeviewExample } from './_components/treeview-example'
import { PageContent } from '@/components/layouts'

/**
 * Props for the treeview showcase page, providing the localized route lang segment.
 */
interface TreeviewPageProps {
  params: Promise<{ lang: string }>
}

/**
 * Server-rendered UI showcase page for the TreeView component.
 * Resolves the localized title and renders the interactive treeview example inside the app page shell.
 */
const TreeviewPage = async ({ params }: TreeviewPageProps) => {
  const { lang } = await params
  const title = (await getServerTranslation(lang, 'page.ui.treeview.title')) || 'UI Tree View'

  return (
    <PageContent title={title} description="Hierarchical data with expand, collapse and checkbox selection." plain>
      <TreeviewExample />
    </PageContent>
  )
}

export default TreeviewPage
