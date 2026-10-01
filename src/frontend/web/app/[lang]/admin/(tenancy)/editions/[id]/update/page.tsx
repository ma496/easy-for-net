import { getServerTranslation } from '@/i18n'
import { EditionUpdateForm } from './_components/edition-update-form'
import { AdminPageContent } from '@/components/layouts'

/**
 * Props for the edition update page, providing the route lang segment and the id of the plan being edited.
 */
interface EditionUpdatePageProps {
  params: Promise<{
    lang: string
    id: string
  }>
}

/**
 * Server-rendered edition update page that resolves the localized title and renders the interactive
 * edition-update form for the specified plan id.
 */
const EditionUpdate = async ({ params }: EditionUpdatePageProps) => {
  const { lang, id } = await params
  const title = await getServerTranslation(lang, 'page.editions.update.title')

  return (
    <AdminPageContent
      title={title}
      innerClassName='max-w-155'
    >
      <EditionUpdateForm editionId={id} />
    </AdminPageContent>
  )
}

export default EditionUpdate
