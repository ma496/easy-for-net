'use client'

import { ArrowLeft } from 'lucide-react'
import { useLocalizedRouter } from '@/hooks'
import { useTranslation } from '@/i18n'
import { Button } from '@/components/ui'

/**
 * The outline Back button that returns one step in the browser history - the status screen's default
 * way on, and usable inside a screen's own `action` beside another link.
 */
export const StatusBackButton = ({ className }: { className?: string }) => {
  const router = useLocalizedRouter()
  const { t } = useTranslation()

  return (
    <Button type="button" variant="outline" size="lg" className={className} icon={<ArrowLeft className="size-4 rtl:-scale-x-100" />} onClick={() => router.back()}>
      {t('common.back') || 'Back'}
    </Button>
  )
}
