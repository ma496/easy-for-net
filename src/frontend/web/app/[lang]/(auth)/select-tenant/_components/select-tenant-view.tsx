'use client'

import { Building2, Check, Loader2 } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { useTenantSwitch } from '@/hooks'
import { useAppSelector } from '@/store/hooks'
import { GetUserInfoTenant } from '@/store/api/identity'
import { cn } from '@/lib/utils'
import { LocalizedLink } from '@/components/ui'

/**
 * Interactive client-side chooser that lists the tenants the signed-in user holds an active membership in and
 * switches the session to the one chosen, without asking them to sign in again. The switch
 * drops every cached record before reading the fresh user info, so nothing belonging to the tenant just left can
 * still be rendered while requests go to the new one.
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
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden bg-white px-6 py-16 dark:bg-[#060818]">
      <div className="relative z-10 w-full max-w-2xl">
        <div className="mb-8 text-center">
          <h1 className="mb-2 text-3xl font-extrabold text-primary uppercase md:text-4xl">
            {t('page.selectTenant.title')}
          </h1>
          <p className="text-base font-bold text-white-dark">{t('page.selectTenant.description')}</p>
        </div>

        {tenants.length === 0 ? (
          // Nothing left to choose from: every tenant this account belonged to is gone, suspended, or has
          // removed it. Signing up creates a tenant, so the way back is a new account rather than a screen
          // here - what this offers is the account's own profile and the door out.
          <div className="rounded-md bg-white p-6 text-center shadow-sm dark:bg-black/20">
            <p className="mb-4 font-medium text-gray-500 dark:text-gray-400">{t('page.selectTenant.emptyDescription')}</p>
            <LocalizedLink href="/profile" className="btn btn-primary">
              {t('page.selectTenant.emptyAction')}
            </LocalizedLink>
          </div>
        ) : (
          <ul className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            {tenants.map((tenant) => (
              <li key={tenant.id}>
                <button
                  type="button"
                  disabled={isBusy}
                  className={cn(
                    'flex w-full cursor-pointer items-center gap-3 rounded-md border border-gray-200 bg-white px-4 py-3 text-start transition-colors hover:border-primary hover:text-primary disabled:cursor-not-allowed disabled:opacity-60 dark:border-gray-600 dark:bg-[#1b2e4b] dark:text-gray-200',
                    tenant.id === activeTenant?.id && 'border-primary bg-primary/10 text-primary'
                  )}
                  onClick={() => switchTenant(tenant)}
                >
                  <Building2 className="h-5 w-5 shrink-0" />
                  <span className="flex flex-1 flex-col truncate">
                    <span className="font-semibold">{tenant.name}</span>
                    <span className="text-xs text-white-dark">{tenant.identifier}</span>
                  </span>
                  {isBusy ? (
                    <Loader2 className="h-4 w-4 shrink-0 animate-spin" />
                  ) : (
                    <Check className={cn('h-4 w-4 shrink-0', tenant.id !== activeTenant?.id && 'invisible')} />
                  )}
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  )
}
