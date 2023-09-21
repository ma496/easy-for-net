'use client'

import Link from "next/link"
import { SubMenu } from "./sub-menu"
import { MenuType } from "./context"

type MenuProps = {
  menu: MenuType
}

const Menu: React.FC<MenuProps> = ({ menu }) => {

  return (
    menu.children && menu.children.length > 0
      ? (
        <div className="flex flex-col gap-1">
          <SubMenu menu={menu} nestedLevel={0} />
        </div>
      )
      : (
        <li>
          <Link href={menu.url ? menu.url : ''} className={menu.isActive ? 'link active' : 'link'}>
            {menu.icon && <menu.icon size={23} className="min-w-max" />}
            {menu.title}
          </Link>
        </li>
      )
  )
}

export { Menu }
