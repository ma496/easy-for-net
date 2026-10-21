import { getServerTranslation } from '@/i18n'
import { SettingsManager } from './_components/settings-manager'
import { PageContent } from '@/components/layouts'

/**
 * Props for the settings page, providing the localized route lang segment.
 */
interface SettingsPageProps {
  params: Promise<{ lang: string }>
}

/**
 * Server-rendered settings page that resolves the localized title and renders the interactive
 * manager - one card per setting, editing the acting scope's own overrides - inside the app page shell.
 */
const SettingsPage = async ({ params }: SettingsPageProps) => {
  const { lang } = await params
  const title = (await getServerTranslation(lang, 'page.settings.title')) || 'Settings'

  return (
    <PageContent title={title} plain>
      <SettingsManager />
    </PageContent>
  )
}

export default SettingsPage
