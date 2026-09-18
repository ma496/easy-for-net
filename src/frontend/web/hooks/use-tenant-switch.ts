'use client'

import { useTranslation } from '@/i18n'
import { useLocalizedRouter } from '@/hooks/use-localized-router'
import { useAppDispatch } from '@/store/hooks'
import { appApi } from '@/store/api/_app-api'
import { useLazyGetUserInfoQuery } from '@/store/api/identity'
import { useTenantExitMutation, useTenantSwitchMutation } from '@/store/api/tenancy'
import { dispatchTenantChanged } from '@/store/tenant-cache'
import { apiErrorAlert, successToast } from '@/lib/utils'

/** What {@link useTenantSwitch} hands back: the two ways the acting tenant changes, and whether one of them is in flight. */
export interface TenantSwitchControls {
  enterTenant: (tenantId: string, fallbackName?: string) => Promise<boolean>
  exitTenant: () => Promise<boolean>
  isBusy: boolean
}

/**
 * The one place the acting tenant changes. Every screen that changes it - the header control, the
 * chooser, and the tenants table a platform user enters a tenant from - drives it through here, so
 * the ordered sequence a change requires is written once rather than repeated and eventually
 * diverged: refuse and stop on an error, drop the whole RTK Query cache, read fresh user info, put
 * the session state and the notification badge back in step with it, then land on the dashboard.
 *
 * The cache is dropped *before* the user info is read, which matters on the failing branch: if that
 * read fails the cache is already empty, so nothing belonging to the tenant just left can still be
 * rendered while requests go to the new one. `dispatchTenantChanged` repeats the reset as the first
 * step of its own sequence, which is idempotent.
 *
 * Both calls report whether they got as far as landing, so a caller with more to do - closing a
 * dropdown, say - can tell a change that happened from one that was refused.
 */
export const useTenantSwitch = (): TenantSwitchControls => {
  const { t } = useTranslation()
  const router = useLocalizedRouter()
  const dispatch = useAppDispatch()

  const [tenantSwitch, { isLoading: isSwitching }] = useTenantSwitchMutation()
  const [tenantExit, { isLoading: isExiting }] = useTenantExitMutation()
  const [getUserInfo, { isLoading: isLoadingUserInfo }] = useLazyGetUserInfoQuery()

  const isBusy = isSwitching || isExiting || isLoadingUserInfo

  // Shared tail of both flows: the session already acts somewhere else by the time this runs, and
  // everything here is about making the browser agree with it.
  const settle = async (messageKey: string, fallbackName?: string): Promise<boolean> => {
    dispatch(appApi.util.resetApiState())

    const userInfoResult = await getUserInfo()
    if (userInfoResult.error) {
      apiErrorAlert(userInfoResult.error)
      return false
    }

    dispatchTenantChanged(dispatch, userInfoResult.data)
    successToast.fire({
      text: t(messageKey, { tenant: userInfoResult.data?.activeTenant?.name ?? fallbackName ?? '' }),
    })
    router.push('/admin')
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

    return settle('page.tenants.switcher.switchSuccess', fallbackName)
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

    return settle('page.tenants.switcher.exitSuccess')
  }

  return { enterTenant, exitTenant, isBusy }
}
