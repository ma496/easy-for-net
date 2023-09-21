'use client'

import { Menu } from "./menu"
import { MenuGroup } from "./menu-group"
import { MenuGroupType, MenuType, useMainLayoutContext } from "./context"
import { usePathname, useRouter } from "next/navigation"
import { useEffect, useState } from "react"
import React from "react"

const doActiveMenuRecursively = (pathName: string, menu: MenuType): void => {
  if (menu.url === pathName) {
    menu.isActive = true
    let parent = menu.parent
    while (parent) {
      parent.isActive = true
      parent = parent.parent
    }
  } else {
    menu.isActive = false
  }

  if (menu.children && menu.children.length > 0) {
    for (const c of menu.children) {
      doActiveMenuRecursively(pathName, c)
    }
  }
}

const doActiveMenu = (pathName: string, menus: (MenuType | MenuGroupType)[]): void => {
  for (const m of menus) {
    if ('group' in m) {
      for (const gm of m.menus) {
        doActiveMenuRecursively(pathName, gm)
      }
    } else {
      doActiveMenuRecursively(pathName, m)
    }
  }
}

const Sidebar = () => {
  const { sidebarOpen, sidebarWidth, headerHeight, getDurationCss, menus, setMenus } = useMainLayoutContext()
  const router = useRouter()
  const pathName = usePathname()
  let isPreviousMenuGroup = false
  const [forceUpdate, setForceUpdate] = useState(false)

  const setMenuState = () => {
    doActiveMenu(pathName, menus)
    setMenus(menus)
    setForceUpdate(prev => !prev)
  }

  useEffect(() => {
    if (menus && menus.length > 0) {
      setMenuState()
    }
  }, [pathName, menus])

  return (
    <div
      className={`fixed bg-background h-screen shadow-lg`}
      style={{
        ...getDurationCss(),
        width: sidebarWidth,
        marginLeft: sidebarOpen ? 0 : -sidebarWidth
      }}>
      <div
        className={`flex items-center gap-2.5 font-medium border-b py-3 mx-3 shadow-none cursor-pointer`}
        style={{ height: headerHeight }}
        onClick={() => router.push('/')}>
        <img
          src="/icons8-easy-48.png"
          width={35}
          alt=""
        />
        <span className="text-xl whitespace-pre">Easy For Net</span>
      </div>
      <div
        className="fixed bg-background shadow-lg h-full overflow-auto scrollbar-thin scrollbar-thumb-secondary-scrollbar"
        style={{ width: sidebarWidth, paddingBottom: headerHeight }}>
        <ul className="whitespace-pre px-2.5 text-[0.9rem] py-5 flex flex-col gap-1 font-medium">
          {
            menus?.map((menu, i) => {
              let output: React.JSX.Element | null | false = null
              if ('menus' in menu) {
                output = <MenuGroup key={i} group={menu} isPreviousMenuGroup={isPreviousMenuGroup} />
                isPreviousMenuGroup = true
              } else {
                output = <Menu key={i} menu={menu} />
                isPreviousMenuGroup = false
              }
              return (
                <React.Fragment key={i}>
                  {output}
                  {/* Use forceUpdate as a key */}
                  {forceUpdate && <div key={`forceUpdate-${i}`} />}
                </React.Fragment>
              )
            })
          }
        </ul>
      </div>
    </div>
  )
}

export { Sidebar }
