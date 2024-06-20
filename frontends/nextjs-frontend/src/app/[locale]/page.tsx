import { Button } from "@/components/ui/button"
import { Sidebar } from "@/components/layout/sidebar"
import { Header } from "@/components/layout/header"
import { Metadata } from "next"
import { useTranslations } from "next-intl"

export const metadata: Metadata = {
  title: 'Dashboard | NextJs Template'
}

export default function HomePage() {
  const t = useTranslations()

  return (
    <div className="grid min-h-screen w-full md:grid-cols-[220px_1fr] lg:grid-cols-[280px_1fr]">
      <Sidebar />
      <div className="flex flex-col">
        <Header />
        <main className="flex flex-1 flex-col gap-4 p-4 lg:gap-6 lg:p-6">
          <div className="flex items-center">
            <h1 className="text-lg font-semibold md:text-2xl">{t('Inventory')}</h1>
          </div>
          <div
            className="flex flex-1 items-center justify-center rounded-lg border border-dashed shadow-sm" x-chunk="dashboard-02-chunk-1"
          >
            <div className="flex flex-col items-center gap-1 text-center">
              <h3 className="text-2xl font-bold tracking-tight">
                {t('NoProducts')}
              </h3>
              <p className="text-sm text-muted-foreground">
                {t('StartSelling')}
              </p>
              <Button className="mt-4">{t('AddProduct')}</Button>
            </div>
          </div>
        </main>
      </div>
    </div>
  )
}
