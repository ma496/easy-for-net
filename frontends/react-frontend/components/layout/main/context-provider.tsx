'use client'

import { ReactNode, useEffect, useState } from "react";
import { MainLayoutContext, MainLayoutContextType } from "./context";
import { useMediaQuery } from "react-responsive";

type MainLayoutProviderProps = {
  children: ReactNode
}

export const MainLayoutProvider: React.FC<MainLayoutProviderProps> = ({ children }: MainLayoutProviderProps) => {
  const isTabletOrMobile = useMediaQuery({ query: '(max-width: 1024px)' })
  const [tabletOrMobile, setTabletOrMobile] = useState(false)
  const [sidebarOpen, setSidebarOpen] = useState(false)

  useEffect(() => {
    setTabletOrMobile(isTabletOrMobile)
    setSidebarOpen(!isTabletOrMobile)
  }, [isTabletOrMobile])

  const contextValue: MainLayoutContextType = {
    tabletOrMobile: tabletOrMobile,
    headerHeight: 64,
    sidebarWidth: 256,
    sidebarOpen: sidebarOpen,
    duration: 500,
    setSidebarOpen: setSidebarOpen,
    setTabletOrMobile: setTabletOrMobile,
    getDurationCss: () => {
      return {
        transitionDuration: `${contextValue.duration}ms`,
        animationDuration: `${contextValue.duration}ms`,
      }
    }
  }

  return (
    <MainLayoutContext.Provider value={contextValue}>{children}</MainLayoutContext.Provider>
  )
};
