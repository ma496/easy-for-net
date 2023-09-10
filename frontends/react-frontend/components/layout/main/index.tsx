'use client'

import React, { useEffect } from "react"
import { Sidebar } from "./sidebar"
import { Header } from "./header"
import { useMainLayoutContext } from "./context"
import { useMediaQuery } from "react-responsive"

type MainLayoutProps = {
  children: React.ReactNode
}

const MainLayout: React.FC<MainLayoutProps> = ({ children }) => {
  const { sidebarOpen, sidebarWidth, headerHeight, getDurationCss, setSidebarOpen, setTabletOrMobile } = useMainLayoutContext()
  const isTabletOrMobile = useMediaQuery({ query: '(max-width: 1024px)' })

  useEffect(() => {
    setTabletOrMobile(isTabletOrMobile)
    setSidebarOpen(!isTabletOrMobile)
  }, [isTabletOrMobile])

  return (
    <div className="flex">
      <Sidebar />
      <div
        className="flex-1 min-h-screen"
        style={{
          ...getDurationCss(),
          marginLeft: sidebarOpen ? sidebarWidth : 0
        }}>
        <Header />
        <div
          className="h-full bg-secondary-background"
          style={{ paddingTop: headerHeight }}>
          {children}
        </div>
      </div>
    </div>
  )
}

export { MainLayout }
