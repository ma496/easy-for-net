import { getServerTranslation } from '@/i18n'
import { PageContent } from '@/components/layouts'
import { AnalyticsDashboard } from './_components/analytics-dashboard'

/**
 * Props for the admin dashboard route, providing the localized route lang segment.
 */
interface DashboardProps {
  params: Promise<{ lang: string }>
}

/**
 * Server-rendered admin dashboard route that resolves the localized title and renders the AnalyticsDashboard
 * component, which lays out its own cards, inside the app page shell.
 */
const Dashboard = async ({ params }: DashboardProps) => {
  const { lang } = await params
  const title = (await getServerTranslation(lang, 'page.dashboard.title')) || 'Dashboard'
  const description = (await getServerTranslation(lang, 'page.dashboard.description')) || 'An overview of visitors, traffic and sign-ups.'

  return (
    <PageContent title={title} description={description} plain>
      <AnalyticsDashboard />
    </PageContent>
  )
}

export default Dashboard
