import { getServerTranslation } from '@/i18n'
import { LocalizationManager } from './_components/localization-manager'
import { AdminPageContent } from '@/components/layouts'

/**
 * Props for the localization admin page, providing the localized route lang segment.
 */
interface LocalizationPageProps {
  params: Promise<{ lang: string }>
}

/**
 * Server-rendered localization admin page that resolves the localized title and renders the
 * interactive manager (the texts and languages tabs) inside the admin page shell.
 */
const LocalizationPage = async ({ params }: LocalizationPageProps) => {
  const { lang } = await params
  const title = await getServerTranslation(lang, 'page.localization.title')

  return (
    <AdminPageContent title={title}>
      <LocalizationManager />
    </AdminPageContent>
  )
}

export default LocalizationPage
