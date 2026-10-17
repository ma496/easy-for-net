'use client'

import { Building2, ShieldCheck } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { useAppSelector } from '@/store/hooks'
import { LocalizedLink, Badge } from '@/components/ui'
import { TenantAddress, TenantLink } from '@/components/custom'
import { AccountShell } from '../../_components/account-shell'

/** The classes every tenant card shares, link or not. */
const cardClassName =
  'group flex w-full items-center gap-3 rounded-xl border border-border bg-surface p-4 text-start shadow-xs transition-colors'

/** The classes a card adds when it is a link to another tenant. */
const linkCardClassName = 'cursor-pointer hover:border-primary/50 hover:bg-surface-2 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none'

/** The icon tile at the start of a card. */
const tileClassName = 'flex size-10 shrink-0 items-center justify-center rounded-lg bg-surface-2 text-muted-foreground transition-colors'

/**
 * Interactive client-side list of the tenants the signed-in account belongs to. Each host keeps its
 * own session, so a tenant is never entered in place: every card is a plain link to that tenant's own
 * address (`tenant.url`), opening in a new tab, and a platform account also gets the platform's address
 * (`platformUrl`). The tenant this host gives the session is shown, not linked. A caller with no
 * tenant left is told so plainly, and an installation that gives tenants no addresses says that instead
 * of listing cards that lead nowhere.
 */
export const SelectTenantView = () => {
  const { t } = useTranslation()
  const tenants = useAppSelector((state) => state.auth.tenants)
  const activeTenant = useAppSelector((state) => state.auth.activeTenant)
  const user = useAppSelector((state) => state.auth.user)

  const platformUrl = user?.isPlatform && activeTenant ? user.platformUrl : null
  const addressed = tenants.filter((tenant) => tenant.url)

  return (
    <AccountShell title={t('page.selectTenant.title') || 'Your tenants'} description={t('page.selectTenant.description') || 'Each tenant opens at its own address, in a new tab.'}>
      {tenants.length === 0 && !platformUrl ? (
        // Nothing to list: every tenant this account belonged to is gone, suspended, or has removed it.
        // Signing up creates a tenant, so the way back is a new account rather than a screen here - what
        // this offers is the account's own profile and the door out.
        <div className="flex flex-col items-center rounded-xl border border-border bg-surface px-6 py-12 text-center shadow-xs">
          <div className="mb-4 flex size-12 items-center justify-center rounded-xl bg-surface-2 text-muted-foreground">
            <Building2 className="size-6" />
          </div>
          <p className="max-w-sm text-sm text-muted-foreground">{t('page.selectTenant.emptyDescription') || 'You do not belong to an active tenant. Ask an administrator to add you to theirs, or sign up to create your own.'}</p>
          <LocalizedLink href="/profile" className="btn btn-primary mt-6">
            {t('page.selectTenant.emptyAction') || 'Go to your profile'}
          </LocalizedLink>
        </div>
      ) : addressed.length === 0 && !platformUrl ? (
        <div className="flex flex-col items-center rounded-xl border border-border bg-surface px-6 py-12 text-center shadow-xs">
          <div className="mb-4 flex size-12 items-center justify-center rounded-xl bg-surface-2 text-muted-foreground">
            <Building2 className="size-6" />
          </div>
          <p className="max-w-sm text-sm text-muted-foreground">{t('page.selectTenant.noAddresses') || 'Tenant addresses are not configured on this installation.'}</p>
        </div>
      ) : (
        <ul className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          {platformUrl && (
            <li>
              <TenantLink href={platformUrl} className={`${cardClassName} ${linkCardClassName}`} arrowClassName="group-hover:text-primary">
                <span className={`${tileClassName} group-hover:text-primary`}>
                  <ShieldCheck className="size-5" />
                </span>
                <span className="flex min-w-0 flex-1 flex-col">
                  <span className="truncate text-sm font-semibold text-foreground">{t('navigation.platformAdministration') || 'Platform administration'}</span>
                  <TenantAddress url={platformUrl} className="text-muted-foreground" />
                </span>
              </TenantLink>
            </li>
          )}
          {addressed.map((tenant) => {
            const isCurrent = tenant.id === activeTenant?.id

            return (
              <li key={tenant.id}>
                {isCurrent || !tenant.url ? (
                  <div aria-current="true" className={`${cardClassName} border-primary bg-primary/5 ring-1 ring-primary/25`}>
                    <span className={`${tileClassName} bg-primary/10 text-primary`}>
                      <Building2 className="size-5" />
                    </span>
                    <span className="flex min-w-0 flex-1 flex-col">
                      <span className="truncate text-sm font-semibold text-foreground">{tenant.name}</span>
                      {tenant.url && <TenantAddress url={tenant.url} className="text-muted-foreground" />}
                    </span>
                    <Badge variant="info" className="shrink-0">
                      {t('page.selectTenant.current') || 'Current'}
                    </Badge>
                  </div>
                ) : (
                  <TenantLink href={tenant.url} className={`${cardClassName} ${linkCardClassName}`} arrowClassName="group-hover:text-primary">
                    <span className={`${tileClassName} group-hover:text-primary`}>
                      <Building2 className="size-5" />
                    </span>
                    <span className="flex min-w-0 flex-1 flex-col">
                      <span className="truncate text-sm font-semibold text-foreground">{tenant.name}</span>
                      <TenantAddress url={tenant.url} className="text-muted-foreground" />
                    </span>
                  </TenantLink>
                )}
              </li>
            )
          })}
        </ul>
      )}
    </AccountShell>
  )
}
