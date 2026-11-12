import { getServerTranslation } from '@/i18n'
import { PageContent } from '@/components/layouts'
import { SeoPageUpdateForm } from '../../_components/seo-page-editors'

/** Props for the SEO update page, providing the route lang segment and the record's id. */
interface SeoUpdatePageProps {
  params: Promise<{ lang: string; id: string }>
}

/** Server-rendered page for editing one page's SEO settings. */
const SeoUpdate = async ({ params }: SeoUpdatePageProps) => {
  const { lang, id } = await params
  const title = (await getServerTranslation(lang, 'page.seo.update.title')) || 'Edit SEO settings'

  return (
    <PageContent title={title} innerClassName="max-w-4xl">
      <SeoPageUpdateForm id={id} />
    </PageContent>
  )
}

export default SeoUpdate
