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
}

const SubMenu: React.FC<SubMenuProps> = ({ menu }) => {
  const pathname = usePathname();
  const [subMenuOpen, setSubMenuOpen] = useState(pathname.includes(menu.url));

  return (
    <>
      <li
        className={`link ${pathname.includes(menu.url) && "active"}`}
        onClick={() => setSubMenuOpen(!subMenuOpen)}
      >
        {menu.icon && <menu.icon size={23} className="min-w-max" />}
        <p className="flex-1 capitalize">{menu.title}</p>
        <IoIosArrowDown
          className={` ${subMenuOpen && "rotate-180"} duration-200 `}
        />
      </li>
      <motion.ul
        animate={
          subMenuOpen
            ? {
              height: "fit-content",
            }
            : {
              height: 0,
            }
        }
        className="flex h-0 flex-col pl-14 text-[0.8rem] font-normal overflow-hidden"
      >
        {menu.children?.map((children, i) => (
          <li key={i}>
            {/* className="hover:text-blue-600 hover:font-medium" */}
            <Link
              href={appendUrl(menu.url, children.url)}
              className={`link capitalize ${pathname === appendUrl(menu.url, children.url) ? "active" : ""}`}
            >
              {children.title}
            </Link>
          </li>
        ))}
      </motion.ul>
    </>
  );
};

export default SubMenu
