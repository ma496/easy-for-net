import { getServerTranslation } from '@/i18n'
import { AdminPageContent } from '@/components/layouts'
import { SaleDashboard } from './_components/sale-dashboard'

/**
 * Props for the admin dashboard route, providing the localized route lang segment.
 */
interface SalesProps {
  params: Promise<{ lang: string }>
}

/**
 * Server-rendered admin dashboard route that resolves the localized title and renders the SaleDashboard
 * component, which lays out its own cards, inside the admin page shell.
 */
const Sales = async ({ params }: SalesProps) => {
  const { lang } = await params
  const title = await getServerTranslation(lang, 'page.dashboard.title')
  const description = await getServerTranslation(lang, 'page.dashboard.description')

  return (
    <AdminPageContent title={title} description={description} plain>
      <SaleDashboard />
    </AdminPageContent>
  )
}

export default Sales
