import { getServerTranslation } from '@/i18n'
import { PageContent } from '@/components/layouts'
import { SeoPageCreateForm } from '../_components/seo-page-editors'

/** Props for the SEO create page, providing the localized route lang segment. */
interface SeoCreatePageProps {
  params: Promise<{ lang: string }>
}

/** Server-rendered page for adding one page's SEO settings. */
const SeoCreate = async ({ params }: SeoCreatePageProps) => {
  const { lang } = await params
  const title = (await getServerTranslation(lang, 'page.seo.create.title')) || 'Add SEO settings'

  return (
    <PageContent title={title} innerClassName="max-w-4xl">
      <SeoPageCreateForm />
    </PageContent>
  )
}

export default SeoCreate
