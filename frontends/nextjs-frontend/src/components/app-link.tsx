import { Link } from "@/navigation"
import { useLocale } from "next-intl"
// import Link from "next/link"
import { usePathname } from "next/navigation"
import React, { ReactNode, useEffect } from "react"

type AppLinkProps = React.ComponentPropsWithoutRef<typeof Link> & {
  children: ReactNode
}

export function AppLink({href, children, ...rest}: AppLinkProps) {
  const locale = useLocale()
  const path = usePathname()
  useEffect(() => {
    console.log(path)
    console.log(locale)
    console.log(href)
    console.log(`${locale}${href}`)
  }, [path])
  const getUrl = () => {
    // if (path.startsWith(`/${locale}`)) {
    //   return href
    // }
    // return `/${locale}${href}`
    return href.toString()
  }

  return (
    <Link
      href={getUrl()}
      {...rest}>
      {children}
    </Link>
  )
}
