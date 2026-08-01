import { Metadata } from 'next'
import { getServerTranslation } from '@/i18n'
import { ServiceUnavailableView } from './_components/service-unavailable-view'

export async function generateMetadata({ params }: { params: Promise<{ lang: string }> }): Promise<Metadata> {
  const { lang } = await params
  return {
    title: await getServerTranslation(lang, 'error.serviceUnavailable.title'),
  }
}

/**
 * Server-rendered service-unavailable (HTTP 503) route that simply delegates
 * to the ServiceUnavailableView component.
 */
const ServiceUnavailablePage = () => {
  return <ServiceUnavailableView />
}

export default ServiceUnavailablePage
