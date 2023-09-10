'use client'

import { ReactNode, useState } from "react";
import { MainLayoutContext, MainLayoutContextType } from "./context";

type MainLayoutProviderProps = {
  children: ReactNode
}

export const MainLayoutProvider: React.FC<MainLayoutProviderProps> = ({ children }: MainLayoutProviderProps) => {
  const [tabletOrMobile, setTabletOrMobile] = useState(false)
  const [sidebarOpen, setSidebarOpen] = useState(false)

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
