'use client'
import { LocalizedLink, Dropdown, type DropdownRef, Loader } from '@/components/ui'
import { useAppSelector } from '@/store/hooks'
import { leaveSignedOut } from '@/store/tenant-cache'
import { useLocalizedRouter } from '@/hooks'
import { useTranslation } from '@/i18n'
import { User, LogOut, Lock } from 'lucide-react'
import { useRef } from 'react'
import { ImagePreview } from './image-preview'
import { useSignoutMutation } from '@/store/api/identity'
import { apiErrorAlert } from '@/lib/utils'

/**
 * Header dropdown that shows the signed-in user avatar, profile/change-password links, and a
 * sign-out action that hits the logout API and then loads the sign-in page afresh, discarding the
 * stored active-tenant selection together with the tenant-scoped data cached in the browser.
 */
export const NavUser = () => {
  const { user } = useAppSelector((state) => state.auth)
  const router = useLocalizedRouter()
  const { t } = useTranslation()
  const isRtl = useAppSelector((state) => state.theme.rtlClass) === 'rtl'
  const dropdownRef = useRef<DropdownRef>(null)
  const handleLinkClick = () => {
    if (dropdownRef.current) {
      dropdownRef.current.close()
    }
  }

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
          <li>
            <LocalizedLink href="/profile" onClick={handleLinkClick}>
              <User size={16} className="shrink-0 text-muted-foreground" />
              {t('navigation.profile')}
            </LocalizedLink>
          </li>
          <li>
            <LocalizedLink href="/change-password" onClick={handleLinkClick}>
              <Lock size={16} className="shrink-0 text-muted-foreground" />
              {t('navigation.changePassword')}
            </LocalizedLink>
          </li>
          <li className="mt-1 border-t border-border pt-1">
            <button type="button" className="text-danger! hover:bg-danger/10!" onClick={signoutAction}>
              {isSigningOut ? <Loader size="sm" className="shrink-0" variant="danger" /> : <LogOut size={16} className="shrink-0 rtl:-scale-x-100" />}
              {t('page.auth.signout')}
            </button>
          </li>
        </ul>
      </Dropdown>
    </div>
  )
}
