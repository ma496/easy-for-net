import { Metadata } from "next"
import { useTranslations } from "next-intl"

export const metadata: Metadata = {
  title: 'Analytics'
}

export default function AnalyticsPage() {
  const t = useTranslations()

  return (
    <div>
      <h5>{t('Analytics')}</h5>
    </div>
  )
}
