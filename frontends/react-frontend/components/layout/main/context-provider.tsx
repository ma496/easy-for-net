'use client'

import { ReactNode, useEffect, useState } from "react";
import { MainLayoutContext, MainLayoutContextType, MenuGroupType, MenuType } from "./context";
import { useMediaQuery } from "react-responsive";
import { menus as configMenus } from "@/navigation.config";
import { MenuGroupModel, MenuModel } from "./models";

type MainLayoutProviderProps = {
  children: ReactNode
}

const convertMenuRecursively = (menuModel: MenuModel, parent: MenuType | undefined = undefined): MenuType => {
  const menuType: MenuType = {
    title: menuModel.title,
    icon: menuModel.icon,
    url: menuModel.url,
    isActive: false,
    parent,
  };

  if (menuModel.children) {
    menuType.children = menuModel.children.map((child) => convertMenuRecursively(child, menuType));
  }

  return menuType;
}

const convertMenus = (input: (MenuModel | MenuGroupModel)[]): (MenuType | MenuGroupType)[] => {
  return input.map(i => {
    if ('group' in i) {
      return {
        group: i.group,
        menus: i.menus.map(m => convertMenuRecursively(m))
      } as MenuGroupType
    } else {
      return convertMenuRecursively(i)
    }
  })
}

export const MainLayoutProvider: React.FC<MainLayoutProviderProps> = ({ children }: MainLayoutProviderProps) => {
  const isTabletOrMobile = useMediaQuery({ query: '(max-width: 1024px)' })
  const [tabletOrMobile, setTabletOrMobile] = useState(false)
  const [sidebarOpen, setSidebarOpen] = useState(false)
  const [menus, setMenus] = useState<(MenuType | MenuGroupType)[]>([])


  useEffect(() => {
    setTabletOrMobile(isTabletOrMobile)
    setSidebarOpen(!isTabletOrMobile)
  }, [isTabletOrMobile])

  useEffect(() => {
    setMenus(convertMenus(configMenus))
  }, [])

  const contextValue: MainLayoutContextType = {
    tabletOrMobile: tabletOrMobile,
    headerHeight: 64,
    sidebarWidth: 256,
    sidebarOpen: sidebarOpen,
    duration: 500,
    menus: menus,
    setSidebarOpen: setSidebarOpen,
    setTabletOrMobile: setTabletOrMobile,
    getDurationCss: () => {
      return {
        transitionDuration: `${contextValue.duration}ms`,
        animationDuration: `${contextValue.duration}ms`,
      }
    },
    setMenus: setMenus
  }

  return (
    <MainLayoutContext.Provider value={contextValue}>{children}</MainLayoutContext.Provider>
  )
};
