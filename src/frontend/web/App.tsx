'use client'
import { PropsWithChildren, useEffect, useState } from 'react'
import { useAppDispatch, useAppSelector } from '@/store/hooks'
import { toggleRTL, toggleTheme, setDarkMode, toggleMenu, toggleLayout, toggleAnimation, toggleNavbar, toggleSemidark, setUserInfo } from '@/store/slices'
import { AppLoading, ServiceUnavailableView } from '@/components/layouts'
import { i18nConfig, Locale } from '@/i18n'
import { useLazyGetUserInfoQuery } from './store/api/identity'
import { isAllowed } from './lib/utils'
import { usePathname, useRouter } from 'next/navigation'
import { getMatchedAuthUrl } from './auth-urls'
import { CookieConsentDialog } from '@/components/custom'
import { useCookieConsent } from '@/hooks'
import defaultThemeConfig from '@/theme.config'

/**
 * Interactive client-side root component that loads the authenticated user, applies the persisted theme/menu/layout preferences, performs route-level permission checks, and conditionally renders the cookie consent dialog.
 */
function App({ children }: PropsWithChildren) {
  const themeConfig = useAppSelector((state) => state.theme)
  const dispatch = useAppDispatch()
  const pathname = usePathname()
  const router = useRouter()
  const authState = useAppSelector((state) => state.auth)
  const isServiceUnavailable = useAppSelector((state) => state.serviceAvailability.isUnavailable)
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
    if (isServiceUnavailable) return
    if (isLoadingUserInfo) return

    if (!authState.isAuthenticated || !authState.user) return

    const pathSegment = pathname.split('/')[1]
    const lang = i18nConfig.locales.includes(pathSegment as Locale) ? pathSegment : i18nConfig.defaultLocale
    const pathToCheck = i18nConfig.locales.includes(pathSegment as Locale) ? pathname.replace(`/${lang}`, '') || '/' : pathname

    const matchedUrl = getMatchedAuthUrl(pathToCheck)
    if (matchedUrl?.permissions && matchedUrl.permissions.length > 0 && !isAllowed(authState, matchedUrl.permissions)) {
      router.replace(lang === i18nConfig.defaultLocale ? '/unauthorized' : `/${lang}/unauthorized`)
    }
  }, [pathname, authState, isLoadingUserInfo, isServiceUnavailable, router])

  useEffect(() => {
    dispatch(toggleTheme(localStorage.getItem('theme') || defaultThemeConfig.theme))
    dispatch(toggleMenu(localStorage.getItem('menu') || defaultThemeConfig.menu))
    dispatch(toggleLayout(localStorage.getItem('layout') || defaultThemeConfig.layout))
    dispatch(toggleAnimation(localStorage.getItem('animation') || defaultThemeConfig.animation))
    dispatch(toggleNavbar(localStorage.getItem('navbar') || defaultThemeConfig.navbar))
    dispatch(toggleSemidark(localStorage.getItem('semidark') || defaultThemeConfig.semidark))

    // eslint-disable-next-line react-hooks/set-state-in-effect
    setIsLoading(false)
  }, [dispatch])

  useEffect(() => {
    if (isLoading) return

    const applyTheme = () => {
      const isDark = themeConfig.theme === 'dark' || (themeConfig.theme === 'system' && window.matchMedia('(prefers-color-scheme: dark)').matches)
      dispatch(setDarkMode(isDark))
      document.body.classList.toggle('dark', isDark)
    }

    localStorage.setItem('theme', themeConfig.theme)
    localStorage.setItem('menu', themeConfig.menu)
    localStorage.setItem('layout', themeConfig.layout)
    localStorage.setItem('rtlClass', themeConfig.rtlClass)
    localStorage.setItem('animation', themeConfig.animation)
    localStorage.setItem('navbar', themeConfig.navbar)
    localStorage.setItem('semidark', String(themeConfig.semidark))
    document.documentElement.setAttribute('dir', themeConfig.rtlClass || 'ltr')
    applyTheme()

    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)')
    if (themeConfig.theme === 'system') mediaQuery.addEventListener('change', applyTheme)
    return () => mediaQuery.removeEventListener('change', applyTheme)
  }, [dispatch, isLoading, themeConfig.animation, themeConfig.layout, themeConfig.menu, themeConfig.navbar, themeConfig.rtlClass, themeConfig.semidark, themeConfig.theme])

  useEffect(() => {
    const pathSegment = pathname.split('/')[1]
    const lang = i18nConfig.locales.includes(pathSegment as Locale) ? pathSegment : i18nConfig.defaultLocale
    const currentLang = themeConfig.languageList.find((language) => language.code === lang)
    dispatch(toggleRTL(currentLang?.isRTL ? 'rtl' : 'ltr'))
  }, [dispatch, pathname, themeConfig.languageList])

  return (
    <div
      className={`${(themeConfig.sidebar && 'toggle-sidebar') || ''} ${themeConfig.menu} ${themeConfig.layout} ${
        themeConfig.rtlClass
      } main-section relative font-nunito text-sm font-normal antialiased`}
    >
      {isServiceUnavailable ? <ServiceUnavailableView /> : isLoading || isLoadingUserInfo ? <AppLoading /> : children}
      {!isServiceUnavailable && showConsentDialog && !consentLoading && <CookieConsentDialog isOpen={true} onAccept={accept} onDecline={decline} />}
    </div>
  )
}

export default App
