import { ProviderComponent, TranslationProvider, LocaleGuard } from '@/components/layouts'
import { Geist, Geist_Mono } from 'next/font/google'
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

/** The app's typefaces, exposed as the CSS variables the design tokens in styles/tailwind.css read. */
const geistSans = Geist({ subsets: ['latin'], display: 'swap', variable: '--font-geist-sans' })
const geistMono = Geist_Mono({ subsets: ['latin'], display: 'swap', variable: '--font-geist-mono' })

/**
 * Runs before first paint so a dark-mode visitor never sees a light flash: reads the saved theme
 * (light, dark or system) and sets the `dark` class on <html> that every color token keys off.
 * App.tsx keeps the class in step afterwards.
 */
const themeScript = `(function(){try{var t=localStorage.getItem('theme')||'system';var d=t==='dark'||(t==='system'&&matchMedia('(prefers-color-scheme: dark)').matches);document.documentElement.classList.toggle('dark',d);}catch(e){}})()`

export async function generateStaticParams() {
  return i18nConfig.locales.map((locale) => ({ lang: locale }))
}

/**
 * Server-rendered root layout for every locale-prefixed route.
 * Loads the locale dictionary, applies the Geist fonts and the saved theme, and wraps the tree in the translation and Redux provider components.
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
      <head>
        <script dangerouslySetInnerHTML={{ __html: themeScript }} />
      </head>
      <body className={`${geistSans.variable} ${geistMono.variable}`} suppressHydrationWarning={true}>
        <TranslationProvider dictionary={dictionary}>
          <LocaleGuard urlLocale={lang} servedCulture={dictionary.culture} defaultCulture={dictionary.defaultCulture} languages={dictionary.languages} />
          <ProviderComponent>{children}</ProviderComponent>
        </TranslationProvider>
      </body>
    </html>
  )
}
