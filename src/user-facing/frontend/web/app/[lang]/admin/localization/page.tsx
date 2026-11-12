import { getServerTranslation } from '@/i18n'
import { LocalizationManager } from './_components/localization-manager'
import { PageContent } from '@/components/layouts'

/**
 * Props for the localization page, providing the localized route lang segment.
 */
interface LocalizationPageProps {
  params: Promise<{ lang: string }>
}

/**
 * Server-rendered localization page that resolves the localized title and renders the
 * interactive manager (the texts and languages tabs) inside the app page shell.
 */
const LocalizationPage = async ({ params }: LocalizationPageProps) => {
  const { lang } = await params
  const title = (await getServerTranslation(lang, 'page.localization.title')) || 'Localization'

  return (
    <PageContent title={title} plain>
      <LocalizationManager />
    </PageContent>
  )
}

export default LocalizationPage
