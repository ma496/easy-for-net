'use client'

import { useEffect, useState, useRef } from "react";
import SubMenu from "./subMenu";
import { motion } from "framer-motion";

// * React icons
import { IoIosArrowBack } from "react-icons/io";
import { useMediaQuery } from "react-responsive";
import { usePathname } from 'next/navigation'
import { MdMenu } from "react-icons/md";
import Menu from "./menu";
import { menus } from "@/navigation.config";
import { MenuGroupModel, MenuModel } from "./models";

const Sidebar = () => {
  let isTabletMid = useMediaQuery({ query: "(max-width: 768px)" });
  const [open, setOpen] = useState(isTabletMid ? false : true);
  const sidebarRef = useRef<HTMLDivElement | null>(null);
  const pathname = usePathname();

  useEffect(() => {
    if (isTabletMid) {
      setOpen(false);
    } else {
      setOpen(true);
    }
  }, [isTabletMid]);

  useEffect(() => {
    isTabletMid && setOpen(false);
  }, [pathname]);

  const Nav_animation = isTabletMid
    ? {
      open: {
        x: 0,
        width: "16rem",
        transition: {
          damping: 40,
        },
      },
      closed: {
        x: -250,
        width: 0,
        transition: {
          damping: 40,
          delay: 0.15,
        },
      },
    }
    : {
      open: {
        width: "16rem",
        transition: {
          damping: 40,
        },
      },
      closed: {
        width: "4rem",
        transition: {
          damping: 40,
        },
      },
    };

  const RenderMenu = ({ menu }: { menu: MenuModel }) => {
    return (
      menu.children && menu.children.length > 0
        ? (open || isTabletMid) && (
          <div className="flex flex-col gap-1">
            <SubMenu menu={menu} />
          </div>
        )
        : (
          <Menu menu={menu} />
        )
    )
  }

  const RenderMenuGroup = ({group}: {group: MenuGroupModel}) => {
    return (
      (open || isTabletMid) && (
        <div className="border-y py-5 border-slate-300 ">
          <small className="pl-3 text-slate-500 inline-block mb-2">
            {group.group}
          </small>
          {group.menus?.map((menu, i) => <RenderMenu key={i} menu={menu} />)}
        </div>
      )
    )
  }

  return (
    <div>
      <div
        onClick={() => setOpen(false)}
        className={`md:hidden fixed inset-0 max-h-screen z-[998] bg-black/50 ${open ? "block" : "hidden"}`}
      ></div>
      <motion.div
        ref={sidebarRef}
        variants={Nav_animation}
        initial={{ x: isTabletMid ? -250 : 0 }}
        animate={open ? "open" : "closed"}
        className=" bg-white text-gray shadow-xl z-[999] max-w-[16rem]  w-[16rem]
            overflow-hidden md:relative fixed
         h-screen "
      >
        <div className="flex items-center gap-2.5 font-medium border-b py-3 border-slate-300  mx-3">
          <img
            src="https://img.icons8.com/color/512/firebase.png"
            width={45}
            alt=""
          />
          <span className="text-xl whitespace-pre">Fireball</span>
        </div>

        <div className="flex flex-col  h-full">
          <ul className="whitespace-pre px-2.5 text-[0.9rem] py-5 flex flex-col gap-1  font-medium overflow-x-hidden scrollbar-thin scrollbar-track-white scrollbar-thumb-slate-100   md:h-[68%] h-[70%]">
            {/* {(open || isTabletMid) && (
              <div className="border-y py-5 border-slate-300 ">
                <small className="pl-3 text-slate-500 inline-block mb-2">
                  Product categories
                </small>
                {menus?.map((menu) => (
                  menu.children && menu.children.length > 0
                    ? (
                      <div key={menu.title} className="flex flex-col gap-1">
                        <SubMenu menu={menu} />
                      </div>
                    )
                    : (
                      <Menu menu={menu} />
                    )
                ))}
              </div>
            )} */}
            {menus?.map((menu, i) => {
              let output: React.JSX.Element | null | false = null
              if ('title' in menu) {
                output = <RenderMenu key={i} menu={menu} />
              } else if ('group' in menu) {
                output = <RenderMenuGroup key={i} group={menu} />
              }
              return output
            })}
          </ul>
          {open && (
            <div className="flex-1 text-sm z-50  max-h-48 my-auto  whitespace-pre   w-full  font-medium  ">
              <div className="flex border-y border-slate-300 p-4 items-center justify-between">
                <div>
                  <p>Spark</p>
                  <small>No-cost $0/month</small>
                </div>
                <p className="text-teal-500 py-1.5 px-3 text-xs bg-teal-50 rounded-xl">
                  Upgrade
                </p>
              </div>
            </div>
          )}
        </div>
        <motion.div
          onClick={() => {
            setOpen(!open);
          }}
          animate={
            open
              ? {
                x: 0,
                y: 0,
                rotate: 0,
              }
              : {
                x: -10,
                y: -200,
                rotate: 180,
              }
          }
          transition={{ duration: 0 }}
          className="absolute w-fit h-fit md:block z-50 hidden right-2 bottom-3 cursor-pointer"
        >
          <IoIosArrowBack size={25} />
        </motion.div>
      </motion.div>
      <div className="m-3 md:hidden" onClick={() => setOpen(true)}>
        <MdMenu size={25} />
      </div>
    </div>
  );
};

export default Sidebar;
