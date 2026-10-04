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

const tenantSwitcherVariants = cva('inline-flex h-9 items-center gap-2 rounded-md border px-2.5 text-sm font-medium transition-colors', {
  variants: {
    interactive: {
      true: 'border-border bg-surface text-foreground shadow-xs hover:bg-surface-2',
      false: 'cursor-default border-transparent bg-surface-2 text-muted-foreground',
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
  // A platform account switches between the tenants it belongs to like anybody else, but its own
  // authority lives in platform scope, and no tenant it could select leads back there: leaving is the
  // only way back. The account tier is the test rather than a permission, because inside a tenant the
  // session carries that tenant's roles and none of the platform's.
  const canExit = !!user?.isPlatform && !!activeTenant

  if (tenants.length === 0 && !activeTenant) {
    return null
  }

  const detailHref = activeTenant ? `/admin/tenants/${activeTenant.id}/detail` : undefined

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
    router.push(`/admin/tenants/${tenantId}/detail`)
  }

  const name = (
    <>
      <Building2 className="h-4 w-4 shrink-0 text-muted-foreground" />
      {/* Below sm only the icon is shown; the name stays available as the control's title. */}
      <span className="hidden max-w-40 truncate sm:inline">{activeTenantName}</span>
    </>
  )

  return (
    <div className={cn('flex items-center gap-1', className)}>
      {canViewDetail && detailHref ? (
        <LocalizedLink href={detailHref} className={tenantSwitcherVariants({ interactive: true })} title={`${activeTenantName} - ${t('page.tenants.switcher.viewDetail')}`}>
          {name}
        </LocalizedLink>
      ) : (
        <div className={tenantSwitcherVariants({ interactive: false })} title={`${t('page.tenants.switcher.label')}: ${activeTenantName}`}>
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
            // Below sm the trigger sits near the start of the header, so an end-anchored menu would run
            // off screen; it spans the viewport under the header instead, as the notification panel does.
            menuClassName="max-sm:fixed max-sm:inset-x-2 max-sm:top-14"
            button={isBusy ? <Loader size="sm" className="shrink-0" /> : <ChevronDown className="h-4 w-4 shrink-0" />}
          >
            <ul className="w-full sm:w-64">
              <li className="px-2.5 pt-1.5 pb-1 text-[11px] font-semibold tracking-wider text-subtle-foreground uppercase">{t('page.tenants.switcher.switchTo')}</li>
              {tenants.map((tenant) => (
                <li key={tenant.id} className="flex items-center">
                  <button
                    type="button"
                    disabled={isBusy}
                    className={cn('min-w-0 flex-1', tenant.id === activeTenant?.id && 'font-medium text-primary!')}
                    onClick={() => switchTenant(tenant)}
                  >
                    <Check className={cn('h-4 w-4 shrink-0', tenant.id !== activeTenant?.id && 'invisible')} />
                    <span className="truncate">{tenant.name}</span>
                  </button>
                  {canViewDetail && (
                    <button
                      type="button"
                      disabled={isBusy}
                      // The dropdown's list styles stretch every row button to full width; this one only holds an icon.
                      className="w-auto! shrink-0 px-2! text-muted-foreground! hover:text-foreground!"
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
          className="icon-btn"
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
