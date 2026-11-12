'use client'
import { PropsWithChildren, useContext, useEffect, useState } from 'react'
import { useAppDispatch, useAppSelector } from '@/store/hooks'
import { toggleRTL, toggleTheme, setDarkMode, setUserInfo, markUserInfoSettled } from '@/store/slices'
import { TranslationContext } from '@/components/layouts/translation-provider'
import { useLazyGetUserInfoQuery } from './store/api/identity'
import { CookieConsentDialog } from '@/components/custom'
import { useCookieConsent } from '@/hooks'
import defaultThemeConfig from '@/theme.config'

/**
 * Interactive client-side root component: it reads the signed-in account (so the public header can show
 * it), applies the persisted theme and direction, and conditionally renders the cookie consent dialog.
 *
 * It never holds rendering back - the public site's server-rendered content is shown as it arrives. The
 * loading screen and the permission redirects live in `AuthGuard`, which only the admin and account
 * layouts render.
 */
function App({ children }: PropsWithChildren) {
  const themeConfig = useAppSelector((state) => state.theme)
  const dispatch = useAppDispatch()
  const dictionary = useContext(TranslationContext)
  const [isThemeRead, setIsThemeRead] = useState(false)
  const [getUserInfo] = useLazyGetUserInfoQuery()
  const { showDialog: showConsentDialog, isLoading: consentLoading, accept, decline } = useCookieConsent()

  useEffect(() => {
    const fetchUserInfo = async () => {
      const result = await getUserInfo()
      if (result.data) {
        dispatch(setUserInfo(result.data))
      }
      // Recorded in the store rather than read off the query's own `isLoading`: the entry is shared with
      // every other `getUserInfo()` call, and one made after a sign-in on an anonymous page (whose entry
      // holds only the 401) reports it loading again - which would swap a guarded page for the loader.
      dispatch(markUserInfoSettled())
    }
    fetchUserInfo()
  }, [getUserInfo, dispatch])

  useEffect(() => {
    dispatch(toggleTheme(localStorage.getItem('theme') || defaultThemeConfig.theme))

    // eslint-disable-next-line react-hooks/set-state-in-effect
    setIsThemeRead(true)
  }, [dispatch])

  useEffect(() => {
    if (!isThemeRead) return

    const applyTheme = () => {
      const isDark = themeConfig.theme === 'dark' || (themeConfig.theme === 'system' && window.matchMedia('(prefers-color-scheme: dark)').matches)
      dispatch(setDarkMode(isDark))
      document.documentElement.classList.toggle('dark', isDark)
    }

    localStorage.setItem('theme', themeConfig.theme)
    localStorage.setItem('rtlClass', themeConfig.rtlClass)
    document.documentElement.setAttribute('dir', themeConfig.rtlClass || 'ltr')
    applyTheme()

    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)')
    if (themeConfig.theme === 'system') mediaQuery.addEventListener('change', applyTheme)
    return () => mediaQuery.removeEventListener('change', applyTheme)
  }, [dispatch, isThemeRead, themeConfig.rtlClass, themeConfig.theme])

  useEffect(() => {
    // The served culture's own RTL flag decides direction, so an override of the enabled languages is
    // honoured exactly as the server resolved it. Until the dictionary names a served language - the
    // empty fallback the provider starts with - direction falls back to left-to-right.
    const servedLanguage = dictionary.languages.find((language) => language.code === dictionary.culture)
    dispatch(toggleRTL(servedLanguage?.isRtl ? 'rtl' : 'ltr'))
  }, [dispatch, dictionary.culture, dictionary.languages])

  return (
    <div className="relative min-h-screen bg-background font-sans text-sm text-foreground antialiased">
      {children}
      {showConsentDialog && !consentLoading && <CookieConsentDialog isOpen={true} onAccept={accept} onDecline={decline} />}
    </div>
  )
}

export default App
