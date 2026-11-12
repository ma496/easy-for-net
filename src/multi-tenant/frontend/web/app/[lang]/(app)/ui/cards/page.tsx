import { getServerTranslation } from '@/i18n'
import { CardsExample } from './_components/cards-example'
import { PageContent } from '@/components/layouts'

/**
 * Props for the cards showcase page, providing the localized route lang segment.
 */
interface CardsPageProps {
  params: Promise<{ lang: string }>
}

/**
 * Server-rendered UI showcase page for the Card component family.
 * Resolves the localized title and renders the interactive cards example inside the app page shell.
 */
const CardsPage = async ({ params }: CardsPageProps) => {
  const { lang } = await params
  const title = (await getServerTranslation(lang, 'page.ui.cards.title')) || 'UI Cards'

  return (
    <PageContent title={title} description="The card surface and the compositions built from its header, content and footer." plain>
      <CardsExample />
    </PageContent>
  )
}

export default CardsPage
