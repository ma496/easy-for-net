import createMiddleware from 'next-intl/middleware'
import { locales } from './config'

export default createMiddleware({
  locales,
  defaultLocale: 'en'
})

export const config = {
  // Match only internationalized pathnames
  matcher: ['/', '/(ur|en)/:path*']
}
