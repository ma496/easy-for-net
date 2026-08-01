import { useTranslation } from '@/i18n'

/**
 * Footer is the small page footer that renders the current year, the brand name, and an "all rights reserved" line using the active locale's translations.
 */
export const Footer = () => {
  const { t } = useTranslation()
  return (
    <div className="mt-auto px-6 pt-0 text-center sm:ltr:text-left sm:rtl:text-right dark:text-white-dark">
      © {new Date().getFullYear()}. {t('brand.name')}. {t('common.allRightsReserved')}
    </div>
  )
}

