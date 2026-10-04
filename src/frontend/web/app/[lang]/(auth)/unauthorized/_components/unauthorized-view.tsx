'use client'

import { useTranslation } from '@/i18n'
import { ShieldAlert } from 'lucide-react'
import { StatusScreen } from '../../../_components/status-screen'

/**
 * Interactive client-side view that displays the 403 access-denied screen: a shield icon, the localized title/message, and a back button.
 */
export const UnauthorizedView = () => {
  const { t } = useTranslation()

  return <StatusScreen code={403} icon={ShieldAlert} title={t('error.403.title')} message={t('error.403.message')} />
}
