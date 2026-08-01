'use client'
import { PropsWithChildren, useEffect, useState } from 'react'
import { useAppDispatch, useAppSelector } from '@/store/hooks'
import { toggleRTL, toggleTheme, toggleMenu, toggleLayout, toggleAnimation, toggleNavbar, toggleSemidark } from '@/store/slices/themeConfigSlice'
import { AppLoading } from '@/components/layouts/app-loading'
import { i18nConfig, Locale } from '@/i18n'
import { setUserInfo } from './store/slices/authSlice'
import { useLazyGetUserInfoQuery } from './store/api/identity/account/account-api'
import { isAllowed } from './lib/utils'
import { usePathname, useRouter } from 'next/navigation'
import { getMatchedAuthUrl } from './auth-urls'
import { CookieConsentDialog } from '@/components/custom/cookie-consent-dialog'
import { useCookieConsent } from '@/hooks/use-cookie-consent'

/**
 * Interactive client-side root component that loads the authenticated user, applies the persisted theme/menu/layout preferences, performs route-level permission checks, and conditionally renders the cookie consent dialog.
 */
function App({ children }: PropsWithChildren) {
  const themeConfig = useAppSelector((state) => state.theme)
  const dispatch = useAppDispatch()
  const pathname = usePathname()
  const router = useRouter()
  const authState = useAppSelector((state) => state.auth)
  const [isLoading, setIsLoading] = useState(true)
  const [getUserInfo, { isLoading: isLoadingUserInfo }] = useLazyGetUserInfoQuery()
  const { showDialog: showConsentDialog, isLoading: consentLoading, accept, decline } = useCookieConsent()

  useEffect(() => {
    const fetchUserInfo = async () => {
      const result = await getUserInfo()
      if (result.data) {
        dispatch(setUserInfo(result.data))
      }
    }
    fetchUserInfo()
  }, [getUserInfo, dispatch])

  useEffect(() => {
    if (isLoadingUserInfo) return

    if (!authState.isAuthenticated || !authState.user) return

    const pathSegment = pathname.split('/')[1]
    const lang = i18nConfig.locales.includes(pathSegment as Locale) ? pathSegment : i18nConfig.defaultLocale
    const pathToCheck = i18nConfig.locales.includes(pathSegment as Locale) ? pathname.replace(`/${lang}`, '') || '/' : pathname

    const matchedUrl = getMatchedAuthUrl(pathToCheck)
    if (matchedUrl?.permissions && matchedUrl.permissions.length > 0 && !isAllowed(authState, matchedUrl.permissions)) {
      router.replace(lang === i18nConfig.defaultLocale ? '/unauthorized' : `/${lang}/unauthorized`)
    }
  }, [pathname, authState, isLoadingUserInfo, router])

  useEffect(() => {
    dispatch(toggleTheme(localStorage.getItem('theme') || themeConfig.theme))
    dispatch(toggleMenu(localStorage.getItem('menu') || themeConfig.menu))
    dispatch(toggleLayout(localStorage.getItem('layout') || themeConfig.layout))
    dispatch(toggleAnimation(localStorage.getItem('animation') || themeConfig.animation))
    dispatch(toggleNavbar(localStorage.getItem('navbar') || themeConfig.navbar))
    dispatch(toggleSemidark(localStorage.getItem('semidark') || themeConfig.semidark))

    // Calculate direction based on URL language
    const pathSegment = pathname.split('/')[1]
    const lang = i18nConfig.locales.includes(pathSegment as Locale) ? pathSegment : i18nConfig.defaultLocale
    const currentLang = themeConfig.languageList.find(l => l.code === lang)
    const direction = currentLang ? (currentLang.isRTL ? 'rtl' : 'ltr') : 'ltr'

    dispatch(toggleRTL(direction))

    // eslint-disable-next-line react-hooks/set-state-in-effect
    setIsLoading(false)
  }, [dispatch, themeConfig.theme, themeConfig.menu, themeConfig.layout, themeConfig.rtlClass, themeConfig.animation, themeConfig.navbar, themeConfig.locale, themeConfig.semidark, pathname, themeConfig.languageList])

  return (
    <div
      className={`${(themeConfig.sidebar && 'toggle-sidebar') || ''} ${themeConfig.menu} ${themeConfig.layout} ${themeConfig.rtlClass
        } main-section relative font-nunito text-sm font-normal antialiased`}
    >
      {isLoading || isLoadingUserInfo ? <AppLoading /> : children}
      {showConsentDialog && !consentLoading && (
        <CookieConsentDialog isOpen={true} onAccept={accept} onDecline={decline} />
      )}
    </div>
  )
}

export default App
