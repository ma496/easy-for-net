import React from "react"
import { MenuGroupModel } from "./models"
import { Menu } from "./menu"

type MenuGroupProps = {
  group: MenuGroupModel
  isPreviousMenuGroup: boolean
}

const MenuGroup: React.FC<MenuGroupProps> = ({ group, isPreviousMenuGroup }) => {
  return (
    <div className={`py-5 ${!isPreviousMenuGroup ? 'border-y border-slate-300' : 'border-b border-slate-300'}`}>
      <small className="pl-3 text-slate-500 inline-block mb-2">
        {group.group}
      </small>
      {group.menus?.map((menu, i) => <Menu key={i} menu={menu} />)}
    </div>
  )
}

export { MenuGroup }
