import { Metadata } from "next"
import { useTranslations } from "next-intl"

export const metadata: Metadata = {
  title: 'Customers'
}

export default function CustomersPage() {
  const t = useTranslations()

  return (
    <div>
      <h5>{t('Customers')}</h5>
    </div>
  )
}
