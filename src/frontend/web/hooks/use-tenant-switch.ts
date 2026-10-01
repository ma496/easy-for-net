'use client'

import { useState } from 'react'
import { useLocalizedRouter } from '@/hooks/use-localized-router'
import { useTenantExitMutation, useTenantSwitchMutation } from '@/store/api/tenancy'
import { leaveForTenantChange } from '@/store/tenant-cache'
import { apiErrorAlert } from '@/lib/utils'

/** What {@link useTenantSwitch} hands back: the two ways the acting tenant changes, and whether one of them is in flight. */
export interface TenantSwitchControls {
  enterTenant: (tenantId: string, fallbackName?: string) => Promise<boolean>
  exitTenant: () => Promise<boolean>
  isBusy: boolean
}

/**
 * The one place the acting tenant changes. Every screen that changes it - the header control, the
 * chooser, and the tenants table a platform user enters a tenant from - drives it through here, so
 * the sequence a change requires is written once rather than repeated and eventually diverged:
 * refuse and stop on an error, otherwise land on the dashboard through a full page load
 * (`leaveForTenantChange`), which discards everything the browser held for the scope just left.
 *
 * Nothing is reset in place: doing so while the page that started the change is still mounted makes
 * its queries refetch under the new session, and one the new scope may not read answers 403 on screen
 * until the navigation completes.
 *
 * Both calls report whether the change was made, so a caller with more to do - closing a dropdown,
 * say - can tell a change that happened from one that was refused. Once one has been made the
 * controls stay busy, since the page is already being replaced.
 */
export const useTenantSwitch = (): TenantSwitchControls => {
  const router = useLocalizedRouter()

  const [tenantSwitch, { isLoading: isSwitching }] = useTenantSwitchMutation()
  const [tenantExit, { isLoading: isExiting }] = useTenantExitMutation()
  const [isLeaving, setIsLeaving] = useState(false)

  const isBusy = isSwitching || isExiting || isLeaving

  const leave = (messageKey: string, tenant: string): true => {
    setIsLeaving(true)
    leaveForTenantChange(router.localize('/admin'), { messageKey, tenant })
    return true
  }

  const enterTenant = async (tenantId: string, fallbackName?: string): Promise<boolean> => {
    if (isBusy) {
      return false
    }

    const switchResult = await tenantSwitch({ tenantId })
    if (switchResult.error) {
      apiErrorAlert(switchResult.error)
      return false
    }

    return leave('page.tenants.switcher.switchSuccess', switchResult.data?.name ?? fallbackName ?? '')
  }

  const exitTenant = async (): Promise<boolean> => {
    if (isBusy) {
      return false
    }

    const exitResult = await tenantExit()
    if (exitResult.error) {
      apiErrorAlert(exitResult.error)
      return false
    }

    return leave('page.tenants.switcher.exitSuccess', '')
  }

  return { enterTenant, exitTenant, isBusy }
}
