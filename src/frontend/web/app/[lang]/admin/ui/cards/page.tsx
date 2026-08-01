import { getServerTranslation } from '@/i18n'
import { CardsExample } from "./_components/cards-example"
import { AdminPageContent } from '@/components/layouts'

/**
 * Props for the cards showcase page, providing the localized route lang segment.
 */
interface CardsPageProps {
  params: Promise<{ lang: string }>
}

/**
 * Server-rendered UI showcase page for the Card component family.
 * Resolves the localized title and renders the interactive cards example inside the admin page shell.
 */
const CardsPage = async ({ params }: CardsPageProps) => {
  const { lang } = await params
  const title = await getServerTranslation(lang, 'page.ui.cards.title')

  return (
    <AdminPageContent title={title}>
      <CardsExample />
    </AdminPageContent>
  )
}

export default CardsPage
