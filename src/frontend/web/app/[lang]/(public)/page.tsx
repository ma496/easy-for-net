import { Metadata } from 'next'
import { getServerTranslation } from '@/i18n'
import Hero from './_components/hero'
import Features from './_components/features'
import CTA from './_components/cta'
import Footer from './_components/footer'

export async function generateMetadata({ params }: { params: Promise<{ lang: string }> }): Promise<Metadata> {
  const { lang } = await params
  return {
    title: await getServerTranslation(lang, 'page.home.title'),
  }
}

/**
 * Server-rendered landing page route under the public route group.
 * Composes the marketing sections (hero, features, call-to-action, footer) into a single full-page layout.
 */
export default async function LandingPage({ params }: { params: Promise<{ lang: string }> }) {
  const { lang } = await params
  return (
    <div className="flex min-h-screen flex-col">
      <Hero />
      <Features lang={lang} />
      <CTA lang={lang} />
      <Footer lang={lang} />
    </div>
  )
}
