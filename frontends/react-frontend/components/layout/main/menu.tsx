'use client'

import Link from "next/link"
import { MenuModel } from "./models"
import { usePathname } from "next/navigation"
import { SubMenu } from "./sub-menu"

type MenuProps = {
  menu: MenuModel
}

const Menu: React.FC<MenuProps> = ({ menu }) => {
  const pathname = usePathname()

  return (
    menu.children && menu.children.length > 0
      ? (
        <div className="flex flex-col gap-1">
          <SubMenu menu={menu} previousUrl={menu.url} nestedLevel={0} />
        </div>
      )
      : (
        <li>
          <Link href={menu.url} className={pathname === menu.url ? 'link active' : 'link'}>
            {menu.icon && <menu.icon size={23} className="min-w-max" />}
            {menu.title}
          </Link>
        </li>
      )
  )
}

export { Menu }
