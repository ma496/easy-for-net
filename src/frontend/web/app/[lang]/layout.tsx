import { ProviderComponent, TranslationProvider } from '@/components/layouts'
import { Nunito } from 'next/font/google'
import { getDictionary, i18nConfig, type Locale, getServerTranslation } from '@/i18n'

import 'react-perfect-scrollbar/dist/css/styles.css'
import '../../styles/tailwind.css'

export async function generateMetadata({ params }: { params: Promise<{ lang: Locale }> }) {
  const { lang } = await params
  const brandName = await getServerTranslation(lang, 'brand.name')

  return {
    title: {
      template: `%s | ${brandName}`,
      default: brandName,
    },
  }
}
/**
 * Configured Nunito Google font instance used as the project's primary typeface and exposed via a CSS variable.
 */
const nunito = Nunito({
  weight: ['400', '500', '600', '700', '800'],
  subsets: ['latin'],
  display: 'swap',
  variable: '--font-nunito',
})

export async function generateStaticParams() {
  return i18nConfig.locales.map((locale) => ({ lang: locale }))
}

/**
 * Server-rendered root layout for every locale-prefixed route.
 * Loads the locale dictionary, applies the Nunito font, and wraps the tree in the translation and Redux provider components.
 */
export default async function RootLayout({
  children,
  params,
}: {
  children: React.ReactNode
  params: Promise<{ lang: string }>
}) {
  const { lang } = await params
  const dictionary = await getDictionary(lang as Locale)

  return (
    <html lang={lang} data-scroll-behavior="smooth" suppressHydrationWarning={true}>
      <head></head>
      <body className={nunito.variable} suppressHydrationWarning={true}>
        <TranslationProvider dictionary={dictionary}>
          <ProviderComponent>{children}</ProviderComponent>
        </TranslationProvider>
      </body>
    </html>
  )
}
