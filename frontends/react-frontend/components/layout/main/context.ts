import { CSSProperties, createContext, useContext } from "react"

export type MainLayoutContextType = {
  tabletOrMobile: boolean
  headerHeight: number
  sidebarWidth: number
  sidebarOpen: boolean
  duration: number,
  setSidebarOpen: (value: boolean) => void
  getDurationCss: () => CSSProperties
}

const MainLayoutContext = createContext<MainLayoutContextType | undefined>(undefined);

const useMainLayoutContext = () => {
  const context = useContext(MainLayoutContext);
  if (!context) {
    throw new Error('useMainLayoutContext must be used within an MainLayoutProvider');
  }
  return context;
};

export { MainLayoutContext, useMainLayoutContext }
