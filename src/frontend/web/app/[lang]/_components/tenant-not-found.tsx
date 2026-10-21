'use client'

import { Building2 } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { NODE_MARK, withNode } from '@/components/custom'
import { StatusScreen } from './status-screen'

/** Props for TenantNotFound: what the API said about the visitor's host. */
interface TenantNotFoundProps {
  /** The platform's own address, to send the visitor to; null when the API could not name it. */
  platformUrl: string | null
  /** The host the browser asked for, named in the message. */
  visitorHost: string | null
}

/**
 * Full-page view for a host that names no tenant - an unknown or deleted one. The root layout renders it
 * in place of every route on such a host, so the visitor sees why there is nothing here rather than a
 * landing page or a sign-in form that cannot succeed, with a link to the main site.
 */
export const TenantNotFound = ({ platformUrl, visitorHost }: TenantNotFoundProps) => {
  const { t } = useTranslation()

  const message = visitorHost
    ? withNode(
        t('page.auth.tenantNotFound.message', { host: NODE_MARK }) || `There is no tenant at ${NODE_MARK}. Check the address, or go to the main site.`,
        <bdi dir="ltr" className="font-mono text-xs break-all text-foreground">
          {visitorHost}
        </bdi>,
      )
    : t('page.auth.tenantNotFound.messageNoHost') || 'There is no tenant at this address. Check the address, or go to the main site.'

  return (
    <StatusScreen
      code={404}
      icon={Building2}
      title={t('page.auth.tenantNotFound.title') || 'Tenant not found'}
      message={message}
      action={
        platformUrl ? (
          <a className="btn btn-primary btn-lg mt-8" href={platformUrl}>
            {t('page.auth.tenantNotFound.action') || 'Go to the main site'}
          </a>
        ) : undefined
      }
    />
  )
}
