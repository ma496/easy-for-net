'use client'

import { Building2, Check, Loader2 } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { useTenantSwitch } from '@/hooks'
import { useAppSelector } from '@/store/hooks'
import { GetUserInfoTenant } from '@/store/api/identity'
import { cn } from '@/lib/utils'
import { LocalizedLink } from '@/components/ui'
import { AccountShell } from '../../_components/account-shell'

/**
 * Interactive client-side chooser that lists the tenants the signed-in user holds an active membership in and
 * switches the session to the one chosen, without asking them to sign in again. The switch
 * lands through a full page load, so nothing belonging to the tenant just left can still be rendered
 * while requests go to the new one.
 */
export const SelectTenantView = () => {
  const { t } = useTranslation()
  const tenants = useAppSelector((state) => state.auth.tenants)
  const activeTenant = useAppSelector((state) => state.auth.activeTenant)

  const { enterTenant, isBusy } = useTenantSwitch()

  const switchTenant = async (tenant: GetUserInfoTenant) => {
    if (tenant.id === activeTenant?.id) {
      return
    }

    await enterTenant(tenant.id, tenant.name)
  }

  return (
    <AccountShell title={t('page.selectTenant.title')} description={t('page.selectTenant.description')}>
      {tenants.length === 0 ? (
        // Nothing left to choose from: every tenant this account belonged to is gone, suspended, or has
        // removed it. Signing up creates a tenant, so the way back is a new account rather than a screen
        // here - what this offers is the account's own profile and the door out.
        <div className="flex flex-col items-center rounded-xl border border-border bg-surface px-6 py-12 text-center shadow-xs">
          <div className="mb-4 flex size-12 items-center justify-center rounded-xl bg-surface-2 text-muted-foreground">
            <Building2 className="size-6" />
          </div>
          <p className="max-w-sm text-sm text-muted-foreground">{t('page.selectTenant.emptyDescription')}</p>
          <LocalizedLink href="/profile" className="btn btn-primary mt-6">
            {t('page.selectTenant.emptyAction')}
          </LocalizedLink>
        </div>
      ) : (
        <ul className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          {tenants.map((tenant) => {
            const isActive = tenant.id === activeTenant?.id

            return (
              <li key={tenant.id}>
                <button
                  type="button"
                  disabled={isBusy}
                  aria-current={isActive ? 'true' : undefined}
                  className={cn(
                    'group flex w-full cursor-pointer items-center gap-3 rounded-xl border border-border bg-surface p-4 text-start shadow-xs transition-colors hover:border-primary/50 hover:bg-surface-2 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none disabled:cursor-not-allowed disabled:opacity-60',
                    isActive && 'border-primary bg-primary/5 ring-1 ring-primary/25 hover:bg-primary/5'
                  )}
                  onClick={() => switchTenant(tenant)}
                >
                  <span className={cn('flex size-10 shrink-0 items-center justify-center rounded-lg bg-surface-2 text-muted-foreground transition-colors group-hover:text-primary', isActive && 'bg-primary/10 text-primary')}>
                    <Building2 className="size-5" />
                  </span>
                  <span className="flex min-w-0 flex-1 flex-col">
                    <span className="truncate text-sm font-semibold text-foreground">{tenant.name}</span>
                    <span className="truncate text-xs text-muted-foreground">{tenant.identifier}</span>
                  </span>
                  {isBusy ? (
                    <Loader2 className="size-4 shrink-0 animate-spin text-muted-foreground" />
                  ) : (
                    <Check className={cn('size-4 shrink-0 text-primary', !isActive && 'invisible')} />
                  )}
                </button>
              </li>
            )
          })}
        </ul>
      )}
    </AccountShell>
  )
}
