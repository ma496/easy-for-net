import { LanguageDropdown } from '@/components/custom/language-dropdown'
import { getServerTranslation } from '@/i18n'

/**
 * Server-rendered site footer used on the public landing page.
 * Displays the brand name, current-year copyright line, and the language switcher.
 */
export const Footer = async ({ lang }: { lang: string }) => {
  const [brandName, allRightsReserved] = await Promise.all([
    getServerTranslation(lang, 'brand.name'),
    getServerTranslation(lang, 'common.allRightsReserved'),
  ])

  return (
    <footer className="bg-white dark:bg-black border-t border-gray-200 dark:border-gray-800">
      <div className="mx-auto max-w-7xl px-4 py-12 sm:px-6 md:flex md:items-center md:justify-between lg:px-8">
        <div className="flex justify-center gap-6 md:order-2">
          <LanguageDropdown />
        </div>
        <div className="mt-8 md:order-1 md:mt-0">
          <p className="text-center text-base text-gray-400">
            © {new Date().getFullYear()}. {brandName}. {allRightsReserved}
          </p>
        </div>
      </div>
    </footer>
  )
}

