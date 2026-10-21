'use client'
import { LocalizedLink, Dropdown, type DropdownRef, Loader } from '@/components/ui'
import { useAppSelector } from '@/store/hooks'
import { leaveSignedOut } from '@/store/tenant-cache'
import { useLocalizedRouter, useOpenTenant } from '@/hooks'
import { useTranslation } from '@/i18n'
import { User, LogOut, Lock, Building2, ShieldCheck, ArrowUpRight } from 'lucide-react'
import { useRef } from 'react'
import { ImagePreview } from './image-preview'
import { TenantLink } from './tenant-link'
import { useSignoutMutation } from '@/store/api/identity'
import { apiErrorAlert } from '@/lib/utils'

/** How many tenant rows the menu lists before it adds a link to the full list. */
const maxMenuTenants = 6

/** One row of the menu's "Other tenants" section: a tenant (by id) or the platform, at its own address. */
interface TenantRow {
  key: string
  name: string
  url: string
  /** The tenant's id, which a sign-in ticket is issued for; absent on the platform's row. */
  tenantId?: string
}

/**
 * Header dropdown that shows the signed-in user avatar, the account's other tenants (each opening at its
 * own address in a new tab, signed in there through a single-use ticket, since every host keeps its own
 * session; the platform's row is a plain link), profile/change-password
 * links, and a sign-out action that hits the logout API and then loads the sign-in page afresh,
 * discarding the tenant-scoped data cached in the browser.
 */
export const NavUser = () => {
  const { user, tenants, activeTenant } = useAppSelector((state) => state.auth)
  const router = useLocalizedRouter()
  const { t } = useTranslation()
  const isRtl = useAppSelector((state) => state.theme.rtlClass) === 'rtl'
  const dropdownRef = useRef<DropdownRef>(null)
  const handleLinkClick = () => {
    if (dropdownRef.current) {
      dropdownRef.current.close()
    }
  }

  // The tenants this account may act in other than the one this host gives it, each only when it has an
  // address; a platform account acting in a tenant also gets the platform's own address back.
  const tenantRows: TenantRow[] = [
    ...tenants
      .filter((tenant) => tenant.url && tenant.id !== activeTenant?.id)
      .map((tenant) => ({ key: tenant.id, name: tenant.name, url: tenant.url as string, tenantId: tenant.id })),
    ...(user?.isPlatform && activeTenant && user.platformUrl
      ? [{ key: 'platform', name: t('navigation.platformAdministration') || 'Platform administration', url: user.platformUrl }]
      : []),
  ]
  const hasMoreTenants = tenantRows.length > maxMenuTenants

  const [signoutApi, { isLoading: isSigningOut }] = useSignoutMutation()
  const { openTenant, isOpening } = useOpenTenant()

  const signInToTenant = (tenantId: string) => {
    handleLinkClick()
    void openTenant(tenantId)
  }

  const signoutAction = async () => {
    if (isSigningOut) {
      return
    }

    const result = await signoutApi()
    if (result.error) {
      apiErrorAlert(result.error)
      return
    }
    // Signing out must leave nothing of this tenant behind, so the next user signing in on this
    // browser inherits neither a selection nor a previous tenant's records: leaveSignedOut loads the
    // sign-in page afresh, discarding the whole store rather than resetting it under a mounted page.
    leaveSignedOut(router.localize('/signin'))
  }

  return (
    <div className="dropdown">
      <Dropdown
        ref={dropdownRef}
        placement={`${isRtl ? 'bottom-start' : 'bottom-end'}`}
        btnClassName="flex size-9 items-center justify-center rounded-full ring-offset-2 ring-offset-background transition hover:ring-2 hover:ring-primary/40"
        button={
          <div className="flex size-8 items-center justify-center overflow-hidden rounded-full bg-primary/12 text-[13px] font-semibold text-primary uppercase">
            {user?.image ? (
              <ImagePreview imageName={user.image} alt="userProfile" className="object-cover" fallback={<span>{user?.username?.charAt(0)}</span>} objectFit="cover" />
            ) : (
              <span>{user?.username?.charAt(0) || <User size={16} />}</span>
            )}
          </div>
        }
      >
        <ul className="w-64">
          <li>
            <div className="mb-1 flex items-center gap-3 border-b border-border px-2.5 pt-2 pb-3">
              <div className="size-10 shrink-0 overflow-hidden rounded-full ring-1 ring-border">
                {user?.image ? (
                  <ImagePreview
                    imageName={user.image}
                    alt="userProfile"
                    className="object-cover saturate-50 group-hover:saturate-100"
                    fallback={<span className="flex size-full items-center justify-center bg-primary/12 text-sm font-semibold text-primary uppercase">{user?.username?.charAt(0)}</span>}
                    objectFit="cover"
                  />
                ) : (
                  <span className="flex size-full items-center justify-center bg-primary/12 text-sm font-semibold text-primary uppercase">{user?.username?.charAt(0)}</span>
                )}
              </div>
              <div className="min-w-0">
                <div className="truncate text-sm font-semibold text-foreground">{user?.username ?? ''}</div>
                <div className="truncate text-xs text-muted-foreground">{user?.email}</div>
              </div>
            </div>
          </li>
          {tenantRows.length > 0 && (
            <li className="mb-1 border-b border-border pb-1">
              <div className="px-2.5 pt-1.5 pb-1 text-[11px] font-semibold tracking-wider text-subtle-foreground uppercase rtl:tracking-normal">{t('navigation.otherTenants') || 'Other tenants'}</div>
              <ul className="max-h-64 overflow-y-auto">
                {(hasMoreTenants ? tenantRows.slice(0, maxMenuTenants) : tenantRows).map((row) => (
                  <li key={row.key}>
                    {row.tenantId ? (
                      <button type="button" onClick={() => signInToTenant(row.tenantId as string)} disabled={isOpening} title={row.name}>
                        <Building2 size={16} className="shrink-0 text-muted-foreground" />
                        <span className="min-w-0 flex-1 truncate text-start">{row.name}</span>
                        <ArrowUpRight aria-hidden="true" className="size-4 shrink-0 text-muted-foreground rtl:-scale-x-100" />
                        <span className="sr-only">{t('common.opensInNewTab') || '(opens in a new tab)'}</span>
                      </button>
                    ) : (
                      <TenantLink href={row.url} onClick={handleLinkClick} title={row.name}>
                        <ShieldCheck size={16} className="shrink-0 text-muted-foreground" />
                        <span className="min-w-0 flex-1 truncate">{row.name}</span>
                      </TenantLink>
                    )}
                  </li>
                ))}
                {hasMoreTenants && (
                  <li>
                    <LocalizedLink href="/select-tenant" onClick={handleLinkClick}>
                      <Building2 size={16} className="shrink-0 text-muted-foreground" />
                      <span className="min-w-0 flex-1 truncate">{t('navigation.allTenants') || 'All tenants'}</span>
                    </LocalizedLink>
                  </li>
                )}
              </ul>
            </li>
          )}
          <li>
            <LocalizedLink href="/profile" onClick={handleLinkClick}>
              <User size={16} className="shrink-0 text-muted-foreground" />
              {t('navigation.profile') || 'Profile'}
            </LocalizedLink>
          </li>
          <li>
            <LocalizedLink href="/change-password" onClick={handleLinkClick}>
              <Lock size={16} className="shrink-0 text-muted-foreground" />
              {t('navigation.changePassword') || 'Change Password'}
            </LocalizedLink>
          </li>
          <li className="mt-1 border-t border-border pt-1">
            <button type="button" className="text-danger! hover:bg-danger/10!" onClick={signoutAction}>
              {isSigningOut ? <Loader size="sm" className="shrink-0" variant="danger" /> : <LogOut size={16} className="shrink-0 rtl:-scale-x-100" />}
              {t('page.auth.signout') || 'Sign Out'}
            </button>
          </li>
        </ul>
      </Dropdown>
    </div>
  )
}
