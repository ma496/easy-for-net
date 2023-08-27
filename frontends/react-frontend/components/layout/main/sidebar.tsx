'use client'

import { menus } from "@/navigation.config"
import { Menu } from "./menu"
import { MenuGroup } from "./menu-group"
import { useMainLayoutContext } from "./context"

const Sidebar = () => {
  const { sidebarOpen, sidebarWidth, headerHeight, getDurationCss } = useMainLayoutContext()
  let isPreviousMenuGroup = false

  return (
    <div
      className={`fixed bg-background h-screen shadow-lg`}
      style={{
        ...getDurationCss(),
        width: sidebarWidth,
        marginLeft: sidebarOpen ? 0 : -sidebarWidth
      }}>
      <div
        className={`flex items-center gap-2.5 font-medium border-b py-3 mx-3 shadow-none`}
        style={{ height: headerHeight }}>
        <img
          src="/icons8-easy-48.png"
          width={45}
          alt=""
        />
        <span className="text-xl whitespace-pre">Easy For Net</span>
      </div>
      <div
        className="fixed bg-background shadow-lg h-full overflow-y-auto scrollbar-thin scrollbar-thumb-secondary-scrollbar"
        style={{ width: sidebarWidth, paddingBottom: headerHeight }}>
        <ul className="whitespace-pre px-2.5 text-[0.9rem] py-5 flex flex-col gap-1 font-medium">
          {
            menus?.map((menu, i) => {
              let output: React.JSX.Element | null | false = null
              if ('title' in menu) {
                output = <Menu key={i} menu={menu} />
                isPreviousMenuGroup = false
              } else if ('group' in menu) {
                output = <MenuGroup key={i} group={menu} isPreviousMenuGroup={isPreviousMenuGroup} />
                isPreviousMenuGroup = true
              }
              return output
            })}
        </ul>
      </div>
    </div>
  )
}

export { Sidebar }
