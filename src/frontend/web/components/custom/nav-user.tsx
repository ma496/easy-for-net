'use client'
import { LocalizedLink } from '@/components/ui'
import { useAppSelector, useAppDispatch } from '@/store/hooks'
import { signout } from '@/store/slices/authSlice'
import { useLocalizedRouter } from '@/hooks/use-localized-router'
import { useTranslation } from '@/i18n'
import { User, LogOut, Lock } from 'lucide-react'
import { Dropdown, DropdownRef } from '../ui/dropdown'
import { useRef } from 'react'
import { ImagePreview } from './image-preview'
import { useSignoutMutation } from '@/store/api/identity'
import { apiErrorAlert } from '@/lib/utils'
import { Loading } from '../ui/loading'

/**
 * Header dropdown that shows the signed-in user avatar, profile/change-password links, and a sign-out action that hits the logout API and redirects to the sign-in page.
 */
export const NavUser = () => {
  const { user } = useAppSelector((state) => state.auth)
  const router = useLocalizedRouter()
  const dispatch = useAppDispatch()
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
    dispatch(signout())
    router.push('/signin')
  }

  return (
    <div className="dropdown w-9 h-9">
      <Dropdown
        ref={dropdownRef}
        placement={`${isRtl ? 'bottom-start' : 'bottom-end'}`}
        btnClassName="block w-9 h-9 p-2 rounded-full bg-white-light/40 dark:bg-dark/40 hover:text-primary hover:bg-white-light/90 dark:hover:bg-dark/60"
        button={
          <div className="w-5 h-5 rounded-full overflow-hidden">
            {user?.image ? (
              <ImagePreview
                imageName={user.image}
                alt="userProfile"
                className="object-cover saturate-50 group-hover:saturate-100"
                fallback={<User className="w-5 h-5" />}
                objectFit="cover"
              />
            ) : (
              <User className="w-5 h-5" />
            )}
          </div>
        }
      >
        <ul className="w-57.5 py-0! font-semibold text-dark dark:text-white-light/90">
          <li>
            <div className="flex items-center px-4 py-4">
              <div className="h-9 w-9 rounded-full overflow-hidden">
                {user?.image ? (
                  <ImagePreview
                    imageName={user.image}
                    alt="userProfile"
                    className="object-cover saturate-50 group-hover:saturate-100"
                    fallback={<img className="h-full w-full object-cover" src="/assets/images/default-avatar.svg" alt="userProfile" />}
                    objectFit="cover"
                  />
                ) : (
                  <img className="h-full w-full object-cover" src="/assets/images/default-avatar.svg" alt="userProfile" />
                )}
              </div>
              <div className="truncate ltr:pl-4 rtl:pr-4">
                <h4 className="text-base">{user?.username ? `${user?.username}` : ''}</h4>
                <button type="button" className="text-black/60 hover:text-primary dark:text-dark-light/60 dark:hover:text-white">
                  {user?.email}
                </button>
              </div>
            </div>
          </li>
          <li>
            <LocalizedLink href="/profile" className="dark:hover:text-white" onClick={handleLinkClick}>
              <User className="h-4.5 w-4.5 shrink-0 ltr:mr-2 rtl:ml-2" />
              {t('navigation.profile')}
            </LocalizedLink>
          </li>
          <li>
            <LocalizedLink href="/change-password" className="dark:hover:text-white" onClick={handleLinkClick}>
              <Lock className="h-4.5 w-4.5 shrink-0 ltr:mr-2 rtl:ml-2" />
              {t('navigation.changePassword')}
            </LocalizedLink>
          </li>
          <li className="cursor-pointer border-t border-white-light dark:border-white-light/10">
            <a
              className="py-3! text-danger"
              onClick={signoutAction}>
              {isSigningOut ? (
                <Loading className="h-4.5 w-4.5 shrink-0 rotate-90 ltr:mr-2 rtl:ml-2" />
              ) : (
                <LogOut className="h-4.5 w-4.5 shrink-0 rotate-90 ltr:mr-2 rtl:ml-2" />
              )}
              {t('page.auth.signout')}
            </a>
          </li>
        </ul>
      </Dropdown>
    </div>
  )
}

