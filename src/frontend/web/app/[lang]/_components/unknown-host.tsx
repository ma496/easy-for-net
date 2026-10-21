'use client'

import { useState, type FormEvent } from 'react'
import { ArrowRight, Building2, Compass, ShieldCheck } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { NODE_MARK, withNode } from '@/components/custom'
import { Button } from '@/components/ui'
import { Input } from '@/components/ui/form'
import { tenantUrlFromTemplate } from '@/lib/utils'
import { StatusScreen } from './status-screen'

/** The shape the API accepts for a tenant identifier, which is also its address's first host label. */
const tenantIdentifierPattern = /^[a-z0-9]+(?:-[a-z0-9]+)*$/

/** Props for UnknownHost: the addresses the API named for the visitor's host. */
interface UnknownHostProps {
  /** The platform's own address. */
  platformUrl: string
  /** The tenant origin template whose first host label is `{tenant}`; null when tenants have no address of their own. */
  tenantUrlTemplate: string | null
  /** The host the browser asked for, named in the message. */
  visitorHost: string | null
}

/**
 * Full-page view for a host that is neither the platform's address nor a tenant's. The root layout
 * renders it in place of every route on such a host, and it offers both ways on: the platform, and a
 * tenant's own address built from the identifier the visitor types.
 */
export const UnknownHost = ({ platformUrl, tenantUrlTemplate, visitorHost }: UnknownHostProps) => {
  const { t } = useTranslation()
  const [identifier, setIdentifier] = useState('')
  const [isInvalid, setIsInvalid] = useState(false)

  const normalized = identifier.trim().toLowerCase()
  const tenantUrl = tenantIdentifierPattern.test(normalized) ? tenantUrlFromTemplate(tenantUrlTemplate, normalized) : null

  const message = visitorHost
    ? withNode(
        t('page.auth.unknownHost.message', { host: NODE_MARK }) || `${NODE_MARK} is not the address of the platform or of a tenant. Choose where to go.`,
        <bdi dir="ltr" className="font-mono text-xs break-all text-foreground">
          {visitorHost}
        </bdi>,
      )
    : t('page.auth.unknownHost.messageNoHost') || 'This is not the address of the platform or of a tenant. Choose where to go.'

  const goToTenant = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!tenantUrl) {
      setIsInvalid(true)
      return
    }
    window.location.assign(tenantUrl)
  }

  return (
    <StatusScreen
      icon={Compass}
      tone="primary"
      title={t('page.auth.unknownHost.title') || 'Where would you like to go?'}
      message={message}
      action={
        <div className="mt-8 flex flex-col gap-3 text-start">
          <section className="rounded-xl border border-border bg-surface p-4 shadow-xs">
            <div className="flex items-start gap-3">
              <div className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-surface-2 text-muted-foreground">
                <ShieldCheck className="size-5" />
              </div>
              <div className="min-w-0 flex-1">
                <h2 className="text-sm font-semibold text-foreground">{t('page.auth.unknownHost.platformTitle') || 'Platform'}</h2>
                <p className="mt-1 text-xs leading-relaxed text-muted-foreground">
                  {t('page.auth.unknownHost.platformDescription') || 'Platform administrators sign in here, and new tenants are created here.'}
                </p>
              </div>
            </div>
            <a className="btn btn-primary mt-4 w-full" href={platformUrl}>
              {t('page.auth.unknownHost.platformAction') || 'Go to the platform'}
              <ArrowRight className="size-4 rtl:-scale-x-100" />
            </a>
          </section>

          {tenantUrlTemplate && (
            <section className="rounded-xl border border-border bg-surface p-4 shadow-xs">
              <div className="flex items-start gap-3">
                <div className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-surface-2 text-muted-foreground">
                  <Building2 className="size-5" />
                </div>
                <div className="min-w-0 flex-1">
                  <h2 className="text-sm font-semibold text-foreground">{t('page.auth.unknownHost.tenantTitle') || 'Tenant'}</h2>
                  <p className="mt-1 text-xs leading-relaxed text-muted-foreground">{t('page.auth.unknownHost.tenantDescription') || "Enter your tenant's identifier to go to its address."}</p>
                </div>
              </div>
              <form className="mt-4 space-y-3" onSubmit={goToTenant} noValidate>
                <Input
                  name="tenantIdentifier"
                  dir={identifier ? 'ltr' : undefined}
                  value={identifier}
                  onChange={(event) => {
                    setIdentifier(event.target.value)
                    setIsInvalid(false)
                  }}
                  placeholder={t('form.placeholder.tenantIdentifier') || 'Enter identifier (for example acme)'}
                  aria-label={t('page.auth.unknownHost.tenantTitle') || 'Tenant'}
                  error={isInvalid ? t('page.auth.unknownHost.tenantInvalid') || 'Use lower-case letters, digits and hyphens.' : undefined}
                />
                {tenantUrl && (
                  <bdi dir="ltr" className="block font-mono text-xs break-all text-muted-foreground">
                    {tenantUrl}
                  </bdi>
                )}
                <Button type="submit" variant="outline" className="w-full">
                  {t('page.auth.unknownHost.tenantAction') || 'Go to tenant'}
                </Button>
              </form>
            </section>
          )}
        </div>
      }
    />
  )
}
