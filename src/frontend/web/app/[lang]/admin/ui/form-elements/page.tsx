import { getServerTranslation } from '@/i18n'
import { FormElementsExample } from "./_components/form-elements-example"
import { AdminPageContent } from '@/components/layouts'

/**
 * Props for the form elements showcase page, providing the localized route lang segment.
 */
interface FormElementsPageProps {
  params: Promise<{ lang: string }>
}

/**
 * Server-rendered UI showcase page for the full set of form input components.
 * Resolves the localized title and renders the interactive form elements example inside the admin page shell.
 */
const FormElementsPage = async ({ params }: FormElementsPageProps) => {
  const { lang } = await params
  const title = await getServerTranslation(lang, 'page.ui.formElements.title')

  return (
    <AdminPageContent title={title}>
      <FormElementsExample />
    </AdminPageContent>
  )
}

export default FormElementsPage
