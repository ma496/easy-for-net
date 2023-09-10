'use client'

import { useState } from "react";
import { motion } from "framer-motion";
import { IoIosArrowDown } from "react-icons/io";
import { usePathname } from 'next/navigation'
import Link from "next/link";

import { MenuModel } from "./models";
import { appendUrl } from "@/lib/utils";

type SubMenuProps = {
  menu: MenuModel
  previousUrl: string
  nestedLevel: number
}

const SubMenu: React.FC<SubMenuProps> = ({ menu, previousUrl, nestedLevel }) => {
  const pathname = usePathname()
  const [subMenuOpen, setSubMenuOpen] = useState(pathname.includes(menu.url))

  return (
    <>
      <li
        className={`${nestedLevel === 0 ? 'link' : 'nested-link'} ${pathname.includes(menu.url) && "active"}`}
        onClick={() => setSubMenuOpen(!subMenuOpen)}
      >
        {menu.icon && <menu.icon size={nestedLevel === 0 ? 23 : 17} className="min-w-max" />}
        <p className="flex-1 capitalize">{menu.title}</p>
        <IoIosArrowDown
          className={`${subMenuOpen && "rotate-180"} duration-200 w-4 min-w-[1rem]`}
        />
      </li>
      <motion.ul
        animate={
          subMenuOpen
            ? {
              height: "fit-content",
              display: "block",
            }
            : {
              height: 0,
              display: "none",
            }
        }
        className={`flex h-0 flex-col text-[0.8rem] font-normal ${nestedLevel === 0 ? 'pl-[40px]' : 'pl-[18px]'}`}
      >
        {menu.children?.map((children, i) => {
          let output: React.JSX.Element | null | boolean = null
          if (!children.children || children.children.length < 1) {
            output = (
              <li
                key={i}>
                <Link
                  href={appendUrl(previousUrl, children.url)}
                  className={`nested-link capitalize ${pathname === appendUrl(previousUrl, children.url) ? "active" : ""}`}
                >
                  {children.icon && <children.icon size={17} className="min-w-max" />}
                  <p className="flex-1 capitalize">{children.title}</p>
                </Link>
              </li>
            )
          } else {
            output = <SubMenu key={i} menu={children} previousUrl={appendUrl(previousUrl, children.url)} nestedLevel={nestedLevel + 1} />
          }
          return output
        })}
      </motion.ul>
    </>
  );
};

export { SubMenu }
