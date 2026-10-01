'use client'

import { useEffect } from 'react'
import { useTranslation } from '@/i18n'
import { successToast } from '@/lib/utils'
import { takeTenantChangeNotice } from '@/store/tenant-cache'

/**
 * Announces a tenant switch or exit on the page it landed on. The change leaves through a full page
 * load (`leaveForTenantChange`), which would take a toast fired beforehand down with the old document,
 * so the message is left in session storage and shown here, once, in the language of the scope entered.
 */
export const TenantChangeNotice = () => {
  const { t } = useTranslation()

  useEffect(() => {
    const notice = takeTenantChangeNotice()
    if (notice) {
      successToast.fire({ text: t(notice.messageKey, { tenant: notice.tenant }) })
    }
    // Read once per document: the notice is consumed on the first render that finds it.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  return null
}
