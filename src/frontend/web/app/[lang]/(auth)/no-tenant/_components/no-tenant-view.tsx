'use client'

import { Building2, LogOut, User } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { useLocalizedRouter } from '@/hooks'
import { useAppDispatch } from '@/store/hooks'
import { dispatchSignedOut } from '@/store/tenant-cache'
import { useSignoutMutation } from '@/store/api/identity'
import { apiErrorAlert } from '@/lib/utils'
import { Button, LocalizedLink } from '@/components/ui'
import { TenantOnboardForm } from './tenant-onboard-form'

/**
 * Interactive client-side screen for an authenticated user holding no usable membership: it explains that they
 * belong to no active tenant (AC-050, AC-126) and offers the three things that stay open to them - account
 * self-service, creating their own tenant (AC-143) and signing out - instead of any tenant-scoped screen.
 */
export const NoTenantView = () => {
  const { t } = useTranslation()
  const router = useLocalizedRouter()
  const dispatch = useAppDispatch()

  const [signoutApi, { isLoading: isSigningOut }] = useSignoutMutation()

  const signoutAction = async () => {
    if (isSigningOut) {
      return
    }

    const result = await signoutApi()
    if (result.error) {
      apiErrorAlert(result.error)
      return
    }
    // Signing out must leave nothing of this account behind, so the next user signing in on this browser
    // inherits neither a selection nor a previous tenant's records.
    dispatchSignedOut(dispatch)
    router.push('/signin')
  }

  return (
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden bg-white px-6 py-16 dark:bg-[#060818]">
      <div className="relative z-10 w-full max-w-xl">
        <div className="mb-8 text-center">
          <div className="mb-4 flex justify-center">
            <Building2 className="h-14 w-14 text-primary" strokeWidth={1.2} />
          </div>
          <h1 className="mb-2 text-3xl font-extrabold text-primary uppercase md:text-4xl">
            {t('page.noTenant.title')}
          </h1>
          <p className="text-base font-bold text-white-dark">{t('page.noTenant.description')}</p>
        </div>

        <div className="rounded-md bg-white p-6 shadow-sm dark:bg-black/20">
          <h2 className="mb-4 text-lg font-semibold text-dark dark:text-white-light">{t('page.noTenant.createTitle')}</h2>
          <TenantOnboardForm />
        </div>

        <div className="mt-6 flex flex-wrap items-center justify-center gap-3">
          <LocalizedLink href="/profile" className="btn btn-outline-primary flex items-center gap-2">
            <User className="h-4 w-4" />
            {t('navigation.profile')}
          </LocalizedLink>
          <Button
            type="button"
            variant="outline"
            onClick={signoutAction}
            isLoading={isSigningOut}
            className="flex items-center gap-2"
          >
            <LogOut className="h-4 w-4" />
            {t('page.auth.signout')}
          </Button>
        </div>
      </div>
    </div>
  )
}
