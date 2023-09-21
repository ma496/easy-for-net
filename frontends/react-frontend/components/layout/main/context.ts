import { CSSProperties, createContext, useContext } from "react"
import { IconType } from "react-icons"

export type MainLayoutContextType = {
  tabletOrMobile: boolean
  headerHeight: number
  sidebarWidth: number
  sidebarOpen: boolean
  duration: number
  menus: (MenuType | MenuGroupType)[]
  setSidebarOpen: (value: boolean) => void
  setTabletOrMobile: (value: boolean) => void
  getDurationCss: () => CSSProperties
  setMenus: (menus: (MenuType | MenuGroupType)[]) => void
}

export type MenuType = {
  title: string
  url?: string
  icon?: IconType
  children?: MenuType[]
  isActive: boolean
  parent?: MenuType
}

export type MenuGroupType = {
  group: string
  menus: MenuType[]
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
