'use client'
import { PropsWithChildren, useEffect } from 'react'
import { usePathname, useRouter } from 'next/navigation'
import { useAppSelector } from '@/store/hooks'
import { i18nConfig, withLocale, withoutLocale, localeFromPathname } from '@/i18n'
import { isAllowed } from '@/lib/utils'
import { decideGuardRedirect } from './guard-redirect'
import { AppLoading } from './app-loading'
import { ServiceUnavailableView } from './service-unavailable-view'

/**
 * The client guard of the areas that need a session - the admin shell and the account screens. It shows
 * the loading screen until this document's account info has answered, the service-unavailable view when
 * the API cannot be reached, and sends a visitor who may not open the screen to sign in or to
 * `/unauthorized`. The public site never renders it, so its pages are never held behind the loader.
 */
export const AuthGuard = ({ children }: PropsWithChildren) => {
  const pathname = usePathname()
  const router = useRouter()
  const authState = useAppSelector((state) => state.auth)
  const isServiceUnavailable = useAppSelector((state) => state.serviceAvailability.isUnavailable)

  const lang = localeFromPathname(pathname)
  const redirect = decideGuardRedirect({
    path: withoutLocale(pathname),
    isSettled: authState.isSettled,
    isAuthenticated: authState.isAuthenticated && !!authState.user,
    isPermitted: (permissions) => isAllowed(authState, permissions),
  })

  useEffect(() => {
    if (isServiceUnavailable || !redirect) return
    // Typed routes cannot know a path that is only composed once the locale segment is put back, so
    // the resolved href is passed untyped - the escape hatch `useLocalizedRouter` uses for the same reason.
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    router.replace((lang === i18nConfig.defaultLocale ? redirect : withLocale(redirect, lang)) as any)
  }, [redirect, isServiceUnavailable, lang, router])

  if (isServiceUnavailable) return <ServiceUnavailableView />
  if (!authState.isSettled || redirect) return <AppLoading />
  return <>{children}</>
}
