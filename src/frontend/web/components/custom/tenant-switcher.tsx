'use client'
import { Dropdown, type DropdownRef, Loader } from '@/components/ui'
import { useLocalizedRouter } from '@/hooks'
import { useTranslation } from '@/i18n'
import { apiErrorAlert, cn, successToast } from '@/lib/utils'
import { appApi } from '@/store/api/_app-api'
import { GetUserInfoTenant, useLazyGetUserInfoQuery } from '@/store/api/identity'
import { useTenantSwitchMutation } from '@/store/api/tenancy'
import { useAppDispatch, useAppSelector } from '@/store/hooks'
import { dispatchTenantChanged } from '@/store/tenant-cache'
import { cva } from 'class-variance-authority'
import { Building2, Check, ChevronDown } from 'lucide-react'
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
 * Application-chrome control that names the tenant the user is acting in and, when the user holds an
 * active membership in another tenant, lets them switch to it without signing in again. It renders
 * nothing for a user who belongs to no tenant and a plain, non-interactive label for a user whose
 * only membership is the tenant already active.
 */
export const TenantSwitcher = ({ className = '' }: TenantSwitcherProps) => {
  const { t } = useTranslation()
  const dispatch = useAppDispatch()
  const router = useLocalizedRouter()
  const isRtl = useAppSelector((state) => state.theme.rtlClass) === 'rtl'
  const activeTenant = useAppSelector((state) => state.auth.activeTenant)
  const tenants = useAppSelector((state) => state.auth.tenants)
  const dropdownRef = useRef<DropdownRef>(null)

  const [tenantSwitch, { isLoading: isSwitching }] = useTenantSwitchMutation()
  const [getUserInfo, { isLoading: isLoadingUserInfo }] = useLazyGetUserInfoQuery()

  const isBusy = isSwitching || isLoadingUserInfo
  // The tenant name is shown persistently; a user whose active tenant was suspended, deleted or
  // whose membership was revoked keeps the chrome and is told that no tenant is selected.
  const activeTenantName = activeTenant?.name ?? t('page.tenants.switcher.noTenant')
  // Switching is offered whenever the list holds a tenant other than the active one - that covers
  // both a user who belongs to several tenants and a user left without an active tenant, who must
  // still be able to pick one of the tenants they belong to.
  const canSwitch = tenants.some((tenant) => tenant.id !== activeTenant?.id)

  if (tenants.length === 0) {
    return null
  }

  const switchTenant = async (tenant: GetUserInfoTenant) => {
    if (isBusy) {
      return
    }
    if (tenant.id === activeTenant?.id) {
      dropdownRef.current?.close()
      return
    }

    const switchResult = await tenantSwitch({ tenantId: tenant.id })
    if (switchResult.error) {
      apiErrorAlert(switchResult.error)
      return
    }

    // The session now carries the new tenant, so every record cached for the previous one is dropped
    // before the fresh user info is read - the order the switch flow prescribes. Resetting first
    // rather than afterwards matters on the failing branch: if the read below fails the cache is
    // already empty, so nothing belonging to the tenant the user has just left can still be rendered
    // while requests go to the new one. tenantChangedActions repeats the reset as the first step of
    // its ordered sequence, which is idempotent.
    dispatch(appApi.util.resetApiState())

    const userInfoResult = await getUserInfo()
    if (userInfoResult.error) {
      apiErrorAlert(userInfoResult.error)
      return
    }

    dropdownRef.current?.close()
    dispatchTenantChanged(dispatch, userInfoResult.data)
    successToast.fire({
      text: t('page.tenants.switcher.switchSuccess', { tenant: userInfoResult.data?.activeTenant?.name ?? tenant.name }),
    })
    router.push('/admin')
  }

  if (!canSwitch) {
    return (
      <div className={cn('dropdown', className)} title={t('page.tenants.switcher.label')}>
        <div className={tenantSwitcherVariants({ interactive: false })}>
          <Building2 className="h-4 w-4 shrink-0" />
          <span className="max-w-40 truncate font-semibold">{activeTenantName}</span>
        </div>
      </div>
    )
  }

  return (
    <div className={cn('dropdown', className)} title={t('page.tenants.switcher.label')}>
      <Dropdown
        ref={dropdownRef}
        placement={`${isRtl ? 'bottom-start' : 'bottom-end'}`}
        isDisabled={isBusy}
        btnClassName={tenantSwitcherVariants({ interactive: true })}
        button={
          <>
            <Building2 className="h-4 w-4 shrink-0" />
            <span className="max-w-40 truncate font-semibold">{activeTenantName}</span>
            {isBusy ? <Loader size="sm" className="shrink-0" /> : <ChevronDown className="h-4 w-4 shrink-0" />}
          </>
        }
      >
        <ul className="w-64 py-0! font-semibold text-dark dark:text-white-light/90">
          <li className="border-b border-white-light px-4 py-2 text-xs font-semibold text-white-dark uppercase dark:border-white-light/10">{t('page.tenants.switcher.switchTo')}</li>
          {tenants.map((tenant) => (
            <li key={tenant.id}>
              <button
                type="button"
                disabled={isBusy}
                className={cn('flex w-full cursor-pointer items-center gap-2 rounded-lg text-start hover:text-primary', tenant.id === activeTenant?.id && 'bg-primary/10 text-primary')}
                onClick={() => switchTenant(tenant)}
              >
                <Check className={cn('h-4 w-4 shrink-0', tenant.id !== activeTenant?.id && 'invisible')} />
                <span className="truncate">{tenant.name}</span>
              </button>
            </li>
          ))}
        </ul>
      </Dropdown>
    </div>
  )
}

export { tenantSwitcherVariants }
