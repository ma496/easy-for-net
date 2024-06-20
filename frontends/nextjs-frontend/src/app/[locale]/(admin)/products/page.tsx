import { Metadata } from "next"
import { useTranslations } from "next-intl"

export const metadata: Metadata = {
  title: 'Products'
}

export default function ProductsPage() {
  const t = useTranslations()

  return (
    <div>
      <h5>{t('Products')}</h5>
    </div>
  )
}
