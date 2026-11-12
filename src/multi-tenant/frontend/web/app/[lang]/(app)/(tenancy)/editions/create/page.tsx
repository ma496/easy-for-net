import { getServerTranslation } from '@/i18n'
import { EditionCreateForm } from './_components/edition-create-form'
import { PageContent } from '@/components/layouts'

/**
 * Props for the edition create page, providing the localized route lang segment.
 */
interface EditionCreatePageProps {
  params: Promise<{ lang: string }>
}

/**
 * Server-rendered edition create page that resolves the localized title and renders the interactive
 * edition-create form within the app page shell.
 */
const EditionCreate = async ({ params }: EditionCreatePageProps) => {
  const { lang } = await params
  const title = (await getServerTranslation(lang, 'page.editions.create.title')) || 'Create Edition'

  return (
    <PageContent
      title={title}
      innerClassName='max-w-3xl'
    >
      <EditionCreateForm />
    </PageContent>
  )
}

export default EditionCreate
