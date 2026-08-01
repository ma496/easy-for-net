'use client'
import { useAppSelector } from '@/store/hooks'
import { usePathname } from 'next/navigation'
import React, { useEffect, useState } from 'react'

/**
 * ContentAnimation is a client component that wraps the main content area and applies the user's chosen entry animation from the theme config, replaying it on every route change.
 */
export const ContentAnimation = ({ children }: { children: React.ReactNode }) => {
  const pathname = usePathname()
  const themeConfig = useAppSelector((state) => state.theme)
  const [animation, setAnimation] = useState(themeConfig.animation)

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setAnimation(themeConfig.animation)
  }, [themeConfig.animation])

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setAnimation(themeConfig.animation)
    setTimeout(() => {
      setAnimation('')
    }, 1100)
  }, [pathname, themeConfig.animation])
  return (
    <>
      {/* BEGIN CONTENT AREA */}
      <div className={`${animation} animate__animated p-6`}>{children}</div>
      {/* END CONTENT AREA */}
    </>
  )
}

