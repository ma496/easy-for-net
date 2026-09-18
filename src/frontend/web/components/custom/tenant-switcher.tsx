'use client'
import { Allow } from '@/allow'
import { Dropdown, type DropdownRef, Loader, LocalizedLink } from '@/components/ui'
import { useLocalizedRouter, useTenantSwitch } from '@/hooks'
import { useTranslation } from '@/i18n'
import { cn, isAllowed } from '@/lib/utils'
import { GetUserInfoTenant } from '@/store/api/identity'
import { useAppSelector } from '@/store/hooks'
import { cva } from 'class-variance-authority'
import { ArrowUpRight, Building2, Check, ChevronDown, LogOut } from 'lucide-react'
import { useRef } from 'react'

const tenantSwitcherVariants = cva('flex items-center gap-2 rounded-lg border px-2 py-1.5 text-sm', {
  variants: {
    interactive: {
      true: 'border-white-dark/30 bg-white text-white-dark hover:border-primary hover:text-primary dark:bg-black',
      false: 'cursor-default border-transparent bg-white-light/40 text-dark dark:bg-dark/40 dark:text-white-light',
    },
  },
  defaultVariants: {
    interactive: true,
  },
})

/** Props for the TenantSwitcher component, allowing the caller to extend the wrapper's classes. */
interface TenantSwitcherProps {
  className?: string
}

/**
 * Application-chrome control naming the tenant the user is acting in. The name is the way into that
 * tenant's detail screen, so the tenant shown in the header is always one click from what it actually
 * is - for a user who belongs to one tenant, to several, and for a platform user who has entered one.
 * Switching is a separate control beside it, offered only when there is somewhere else to go, so
 * opening the detail screen and changing tenants can never be mistaken for one another.
 *
 * It renders nothing for a user who belongs to no tenant and is acting in none, and falls back to a
 * plain, non-interactive label for a user who may not read tenant detail or has no tenant active.
 */
export const TenantSwitcher = ({ className = '' }: TenantSwitcherProps) => {
  const { t } = useTranslation()
  const router = useLocalizedRouter()
  const isRtl = useAppSelector((state) => state.theme.rtlClass) === 'rtl'
  const authState = useAppSelector((state) => state.auth)
  const { activeTenant, tenants, user } = authState
  const dropdownRef = useRef<DropdownRef>(null)

  const { enterTenant, exitTenant, isBusy } = useTenantSwitch()

  const canViewDetail = isAllowed(authState, [Allow.Tenant_Detail])
  // The tenant name is shown persistently; a user whose active tenant was suspended, deleted or
  // whose membership was revoked keeps the chrome and is told that no tenant is selected.
  const activeTenantName = activeTenant?.name ?? t('page.tenants.switcher.noTenant')
  // Switching is offered whenever the list holds a tenant other than the active one - that covers
  // both a user who belongs to several tenants and a user left without an active tenant, who must
  // still be able to pick one of the tenants they belong to.
  const canSwitch = tenants.some((tenant) => tenant.id !== activeTenant?.id)
  // A platform user holds no membership anywhere, so the list is empty for them while they are inside
  // a tenant they entered. Leaving is how they get back out, and it is the only way back: there is no
  // other tenant of theirs to select their way out through.
  const canExit = !!user?.isPlatformAdministrator && !!activeTenant

  if (tenants.length === 0 && !activeTenant) {
    return null
  }

  const detailHref = activeTenant ? `/admin/tenants/detail/${activeTenant.id}` : undefined

  const switchTenant = async (tenant: GetUserInfoTenant) => {
    if (tenant.id === activeTenant?.id) {
      dropdownRef.current?.close()
      return
    }

    if (await enterTenant(tenant.id, tenant.name)) {
      dropdownRef.current?.close()
    }
  }

  const openDetail = (tenantId: string) => {
    dropdownRef.current?.close()
    router.push(`/admin/tenants/detail/${tenantId}`)
  }

  const name = (
    <>
      <Building2 className="h-4 w-4 shrink-0" />
      <span className="max-w-40 truncate font-semibold">{activeTenantName}</span>
    </>
  )

  return (
    <div className={cn('flex items-center gap-1', className)}>
      {canViewDetail && detailHref ? (
        <LocalizedLink href={detailHref} className={tenantSwitcherVariants({ interactive: true })} title={t('page.tenants.switcher.viewDetail')}>
          {name}
        </LocalizedLink>
      ) : (
        <div className={tenantSwitcherVariants({ interactive: false })} title={t('page.tenants.switcher.label')}>
          {name}
        </div>
      )}

      {canSwitch && (
        <div className="dropdown">
          <Dropdown
            ref={dropdownRef}
            placement={`${isRtl ? 'bottom-start' : 'bottom-end'}`}
            isDisabled={isBusy}
            btnClassName={tenantSwitcherVariants({ interactive: true })}
            button={isBusy ? <Loader size="sm" className="shrink-0" /> : <ChevronDown className="h-4 w-4 shrink-0" />}
          >
            <ul className="w-64 py-0! font-semibold text-dark dark:text-white-light/90">
              <li className="border-b border-white-light px-4 py-2 text-xs font-semibold text-white-dark uppercase dark:border-white-light/10">{t('page.tenants.switcher.switchTo')}</li>
              {tenants.map((tenant) => (
                <li key={tenant.id} className="flex items-center">
                  <button
                    type="button"
                    disabled={isBusy}
                    className={cn('flex flex-1 cursor-pointer items-center gap-2 rounded-lg text-start hover:text-primary', tenant.id === activeTenant?.id && 'bg-primary/10 text-primary')}
                    onClick={() => switchTenant(tenant)}
                  >
                    <Check className={cn('h-4 w-4 shrink-0', tenant.id !== activeTenant?.id && 'invisible')} />
                    <span className="truncate">{tenant.name}</span>
                  </button>
                  {canViewDetail && (
                    <button
                      type="button"
                      disabled={isBusy}
                      className="shrink-0 cursor-pointer rounded-lg p-1 text-white-dark hover:text-primary"
                      title={t('page.tenants.switcher.viewDetail')}
                      aria-label={t('page.tenants.switcher.viewDetail')}
                      onClick={() => openDetail(tenant.id)}
                    >
                      <ArrowUpRight className="h-4 w-4" />
                    </button>
                  )}
                </li>
              ))}
            </ul>
          </Dropdown>
        </div>
      )}

      {canExit && (
        <button
          type="button"
          disabled={isBusy}
          className={cn(tenantSwitcherVariants({ interactive: true }), 'cursor-pointer disabled:cursor-not-allowed disabled:opacity-60')}
          title={t('page.tenants.switcher.exitTenant')}
          aria-label={t('page.tenants.switcher.exitTenant')}
          onClick={() => exitTenant()}
        >
          {isBusy ? <Loader size="sm" className="shrink-0" /> : <LogOut className="h-4 w-4 shrink-0" />}
        </button>
      )}
    </div>
  )
}

export { tenantSwitcherVariants }
