import { getServerTranslation } from '@/i18n'
import { PageContent } from '@/components/layouts'
import { SeoPageTable } from './_components/seo-page-table'

/** Props for the SEO list page, providing the localized route lang segment. */
interface SeoPageProps {
  params: Promise<{ lang: string }>
}

/** Server-rendered SEO list page: the per-page SEO records, inside the admin page shell. */
const Seo = async ({ params }: SeoPageProps) => {
  const { lang } = await params
  const [title, description] = await Promise.all([
    getServerTranslation(lang, 'page.seo.title').then((text) => text || 'SEO'),
    getServerTranslation(lang, 'page.seo.description').then((text) => text || 'Titles, descriptions, sharing cards and indexing for each public page. Site-wide defaults are on the Settings screen.'),
  ])

  return (
    <PageContent title={title} description={description}>
      <SeoPageTable />
    </PageContent>
  )
}

export default Seo
