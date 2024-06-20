import { Metadata } from "next"
import { useTranslations } from "next-intl"

export const metadata: Metadata = {
  title: 'Orders'
}

export default function OrdersPage() {
  const t = useTranslations()

  return (
    <div>
      <h5>{t('Orders')}</h5>
    </div>
  )
}
