'use client'
import { useAppSelector } from '@/store/hooks'
import React from 'react'

/**
 * Outer client-side wrapper that applies the configured navbar style and default text colors to its children.
 */
export const MainContainer = ({ children }: { children: React.ReactNode }) => {
  const themeConfig = useAppSelector((state) => state.theme)
  return <div className={`${themeConfig.navbar} main-container text-black dark:text-white-dark`}> {children}</div>
}

