import { getServerTranslation } from '@/i18n'
import { ButtonsExample } from './_components/buttons-example'
import { PageContent } from '@/components/layouts'

/**
 * Props for the buttons showcase page, providing the localized route lang segment.
 */
interface ButtonsPageProps {
  params: Promise<{ lang: string }>
}

/**
 * Server-rendered UI showcase page for the Button and IconButton components.
 * Resolves the localized title and renders the interactive buttons example inside the app page shell.
 */
const ButtonsPage = async ({ params }: ButtonsPageProps) => {
  const { lang } = await params
  const title = (await getServerTranslation(lang, 'page.ui.buttons.title')) || 'UI Buttons'

  return (
    <PageContent title={title} description="Buttons, icon buttons and badges in every variant, size and state." plain>
      <ButtonsExample />
    </PageContent>
  )
}

export default ButtonsPage
