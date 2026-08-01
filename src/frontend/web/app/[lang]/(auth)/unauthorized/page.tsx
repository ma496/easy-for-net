import { Metadata } from 'next'
import { getServerTranslation } from '@/i18n'
import { UnauthorizedView } from './_components/unauthorized-view'

export async function generateMetadata({ params }: { params: Promise<{ lang: string }> }): Promise<Metadata> {
  const { lang } = await params
  return {
    title: await getServerTranslation(lang, 'page.auth.unauthorized.title'),
  }
}

/**
 * Server-rendered unauthorized (HTTP 403) route that simply delegates to the UnauthorizedView component to display the access-denied screen.
 */
const UnauthorizedPage = () => {
  return <UnauthorizedView />
}

export default UnauthorizedPage
