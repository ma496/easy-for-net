'use client'

import Link from "next/link"
import { MenuModel } from "./menuModel"
import { usePathname } from "next/navigation"

type MenuProps = {
  menu: MenuModel
}

const Menu: React.FC<MenuProps> = ({ menu }) => {
  const pathname = usePathname()

  return (
    <li>
      <Link href={menu.url} className={pathname === menu.url ? 'link active' : 'link'}>
        {menu.icon && <menu.icon size={23} className="min-w-max" />}
        {menu.title}
      </Link>
    </li>
  )
}

export default Menu
