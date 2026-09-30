import { getServerTranslation } from '@/i18n'
import { SettingsManager } from './_components/settings-manager'
import { AdminPageContent } from '@/components/layouts'

/**
 * Props for the settings admin page, providing the localized route lang segment.
 */
interface SettingsPageProps {
  params: Promise<{ lang: string }>
}

/**
 * Server-rendered settings admin page that resolves the localized title and renders the interactive
 * manager - one card per setting, editing the acting scope's own overrides - inside the admin page shell.
 */
const SettingsPage = async ({ params }: SettingsPageProps) => {
  const { lang } = await params
  const title = await getServerTranslation(lang, 'page.settings.title')

  return (
    <AdminPageContent title={title}>
      <SettingsManager />
    </AdminPageContent>
  )
}

export default SettingsPage
