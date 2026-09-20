'use client'
import { PropsWithChildren, useEffect, useState } from 'react'
import { useAppDispatch, useAppSelector } from '@/store/hooks'
import { toggleRTL, toggleTheme, setDarkMode, toggleMenu, toggleLayout, toggleAnimation, toggleNavbar, toggleSemidark, setUserInfo } from '@/store/slices'
import { AppLoading, ServiceUnavailableView } from '@/components/layouts'
import { i18nConfig, Locale } from '@/i18n'
import { useLazyGetUserInfoQuery } from './store/api/identity'
import { isAllowed, isPathAvailable, isTenantScopedPath, resolvePlatformLanding, resolveTenantLanding } from './lib/utils'
import { usePathname, useRouter } from 'next/navigation'
import { getMatchedAuthUrl } from './auth-urls'
import { CookieConsentDialog } from '@/components/custom'
import { useCookieConsent } from '@/hooks'
import defaultThemeConfig from '@/theme.config'

/**
 * Interactive client-side root component that loads the authenticated user, applies the persisted theme/menu/layout preferences, keeps the caller off screens they may not open - whether for want of a
 * permission or of a usable tenant - and conditionally renders the cookie consent dialog.
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
    // Every redirect below is decided on the locale-stripped path and put back behind the locale segment here, so a guard never drops the user out of the language they are reading the app in.
    const localized = (target: string) => (lang === i18nConfig.defaultLocale ? target : `/${lang}${target}`)
    // Typed routes cannot know a path that is only composed once the locale segment is put back, so
    // the resolved href is passed untyped - the escape hatch `useLocalizedRouter` uses for the same reason.
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const go = (target: string) => router.replace(localized(target) as any)

    // A platform administrator acting in no tenant needs none: users, roles and notifications answer about the platform's own, and the tenants screens and the dashboard read no tenant data, so the
    // lack of a selection never sends them to the chooser. Only a screen that genuinely needs a tenant - one not listed in tenant-routing - lands them on the dashboard instead. They can still pick a
    // tenant from the header's switcher.
    const platformLanding = resolvePlatformLanding(authState.user)
    if (platformLanding && !isPathAvailable(authState.user, pathToCheck)) {
      go(platformLanding)
      return
    }

    // Opening a screen that only means anything inside a tenant while the session names none - because the tenant it was working in was suspended, deleted or left, and the last renewal dropped it -
    // lands on the chooser instead of a tenant-scoped screen with no tenant behind it. The renewal re-reads the account info, which is how the state below comes to be seen at all. Account
    // self-service, the platform tenancy screens, /unauthorized and the public routes are not tenant-scoped, so they stay reachable throughout.
    if (isTenantScopedPath(pathToCheck) && !platformLanding) {
      const tenantLanding = resolveTenantLanding(authState.user)
      if (tenantLanding) {
        go(tenantLanding)
        return
      }
    }

    // A screen belonging to the other scope is not a refusal but a wrong turn, so it lands on the dashboard rather than on /unauthorized: the permissions the platform's own screens declare are
    // platform-scoped and a tenant session never carries them, so a caller acting in a tenant is not short of a grant anybody could give them. It is also the moment a platform account enters a tenant
    // from the tenants table - the page it entered from belongs to the scope it has just left - and answering that with a refusal would make a successful switch read as a failure.
    if (!platformLanding && !isPathAvailable(authState.user, pathToCheck)) {
      go('/admin')
      return
    }

    const matchedUrl = getMatchedAuthUrl(pathToCheck)
    if (matchedUrl?.permissions && matchedUrl.permissions.length > 0 && !isAllowed(authState, matchedUrl.permissions)) {
      go('/unauthorized')
    }
  }, [pathname, authState, isLoadingUserInfo, isServiceUnavailable, router, dispatch])

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
