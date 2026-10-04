'use client'

import { useTranslation } from '@/i18n'
import { FileQuestion } from 'lucide-react'
import { StatusScreen } from './_components/status-screen'

/**
 * Interactive client-side 404 not-found view that displays an icon, the localized title and message, and a back button to return to the previous page.
 */
const NotFound = () => {
  const { t } = useTranslation()

  return <StatusScreen code={404} icon={FileQuestion} title={t('error.404.title')} message={t('error.404.message')} />
}

export default NotFound
