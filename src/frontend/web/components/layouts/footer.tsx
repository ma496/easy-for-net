import { useTranslation } from '@/i18n'

/**
 * Footer is the small page footer that renders the current year, the brand name, and an "all rights reserved" line using the active locale's translations.
 */
export const Footer = () => {
  const { t } = useTranslation()
  return (
    <footer className="mx-auto w-full max-w-[1400px] px-4 pb-6 text-xs text-subtle-foreground sm:px-6 lg:px-8">
      <div className="border-t border-border pt-5">
        © {new Date().getFullYear()} {t('brand.name')}. {t('common.allRightsReserved')}
      </div>
    </footer>
  )
}
