'use client'
import { PropsWithChildren, useEffect, useState } from 'react'
import { useAppDispatch, useAppSelector } from '@/store/hooks'
import { toggleRTL, toggleTheme, setDarkMode, toggleMenu, toggleLayout, toggleAnimation, toggleNavbar, toggleSemidark, setUserInfo, clearTenantError } from '@/store/slices'
import { AppLoading, ServiceUnavailableView } from '@/components/layouts'
import { i18nConfig, Locale } from '@/i18n'
import { useLazyGetUserInfoQuery } from './store/api/identity'
import { isAllowed, isPathAvailable, isTenantScopedPath, resolvePlatformAdministratorLanding, resolveTenantLanding } from './lib/utils'
import { usePathname, useRouter } from 'next/navigation'
import { getMatchedAuthUrl } from './auth-urls'
import { CookieConsentDialog } from '@/components/custom'
import { useCookieConsent } from '@/hooks'
import defaultThemeConfig from '@/theme.config'

/**
 * Interactive client-side root component that loads the authenticated user, applies the persisted theme/menu/layout preferences, keeps the caller off screens they may not open - whether for want of a
 * permission or of a usable tenant - reacts to a tenant that goes away mid-session, and conditionally renders the cookie consent dialog.
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

    // A platform administrator acting in no tenant needs none: users, roles and notifications answer about the platform's own, and the tenants screens and the dashboard read no tenant data, so
    // neither a tenant refusal nor the lack of a selection sends them to the no-tenant screen or the chooser. A refusal is dropped while they stay where they are; only a screen that genuinely needs a
    // tenant - one not listed in tenant-routing, or the no-tenant screen - lands them on the dashboard instead. They can still pick a tenant from the header's switcher.
    const platformLanding = resolvePlatformAdministratorLanding(authState.user)
    if (platformLanding) {
      if (authState.tenantError) {
        dispatch(clearTenantError())
      }
      if (pathToCheck === '/no-tenant' || !isPathAvailable(authState.user, pathToCheck)) {
        go(platformLanding)
        return
      }
    }

    // A tenant-scoped request the API refused because the session's own tenant went away - it was suspended or deleted, or the membership in it was revoked - is reported by the error middleware as a
    // code on the auth slice rather than by the calling screen. The session stays valid and every other tenant the user holds stays reachable: the user is sent to the chooser, carrying the reason so
    // the screen can say what happened instead of leaving them staring at a failure, or to the no-tenant screen when there is nothing left to choose. Signing them out is never the answer here.
    //
    // The code is held until they have arrived and only cleared there: clearing it while the replace is still in flight would let the rule below run once more on the path being left and decide a
    // plainer destination, dropping the reason. The banner keeps showing after the clear because it is driven by the query string, not by this code.
    if (authState.tenantError && !platformLanding) {
      if (pathToCheck === '/select-tenant' || pathToCheck === '/no-tenant') {
        dispatch(clearTenantError())
        return
      }

      const failureLanding = authState.tenants.length > 0 ? `/select-tenant?reason=${encodeURIComponent(authState.tenantError)}` : '/no-tenant'
      go(failureLanding)
      return
    }

    // Opening a screen that only means anything inside a tenant while no usable selection stands - an account that belongs to none, several memberships with no choice made yet, or a stored selection
    // naming a tenant that has since been suspended, deleted or lost its membership - lands on the no-tenant screen or the chooser instead of a tenant-scoped screen with no tenant behind it. Account
    // self-service, the platform tenancy screens, /unauthorized and the public routes are not tenant-scoped, so they stay reachable throughout.
    if (isTenantScopedPath(pathToCheck) && !platformLanding) {
      const tenantLanding = resolveTenantLanding(authState.user)
      if (tenantLanding) {
        go(tenantLanding)
        return
      }
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
