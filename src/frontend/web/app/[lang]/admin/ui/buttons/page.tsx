import { getServerTranslation } from '@/i18n'
import { ButtonsExample } from "./_components/buttons-example"
import { AdminPageContent } from '@/components/layouts'

/**
 * Props for the buttons showcase page, providing the localized route lang segment.
 */
interface ButtonsPageProps {
  params: Promise<{ lang: string }>
}

/**
 * Server-rendered UI showcase page for the Button and IconButton components.
 * Resolves the localized title and renders the interactive buttons example inside the admin page shell.
 */
const ButtonsPage = async ({ params }: ButtonsPageProps) => {
  const { lang } = await params
  const title = await getServerTranslation(lang, 'page.ui.buttons.title')

  return (
    <AdminPageContent title={title}>
      <ButtonsExample />
    </AdminPageContent>
  )
}

export default ButtonsPage
