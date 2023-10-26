'use client';

import { useEffect, useState } from 'react';
import { IoIosArrowDown } from 'react-icons/io';
import Link from 'next/link';

import { MenuType } from './context';

type SubMenuProps = {
  menu: MenuType;
  nestedLevel: number;
};

const SubMenu: React.FC<SubMenuProps> = ({ menu, nestedLevel }) => {
  const [subMenuOpen, setSubMenuOpen] = useState(menu.isActive);

  useEffect(() => {
    setSubMenuOpen(menu.isActive);
  }, [menu.isActive]);

  return (
    <>
      <li
        className={`${
          nestedLevel === 0 ? 'link' : 'nested-link'
        } ${menu.isActive && 'active'}`}
        onClick={() => setSubMenuOpen(!subMenuOpen)}
      >
        {menu.icon && (
          <menu.icon size={nestedLevel === 0 ? 23 : 17} className="min-w-max" />
        )}
        <p className="flex-1 capitalize">{menu.title}</p>
        <IoIosArrowDown
          className={`${
            subMenuOpen ? 'rotate-180' : ''
          } duration-200 w-4 min-w-[1rem]`}
        />
      </li>
      <ul
        className={`${
          subMenuOpen
            ? 'block animate__animated animate__fadeIn'
            : 'hidden'
        } flex flex-col text-[0.8rem] font-normal ${
          nestedLevel === 0 ? 'pl-[40px]' : 'pl-[18px]'
        }`}
      >
        {menu.children?.map((children, i) => {
          let output: React.JSX.Element | null | boolean = null;
          if (!children.children || children.children.length < 1) {
            output = (
              <li key={i}>
                <Link
                  href={children.url ? children.url : ''}
                  className={`nested-link capitalize ${
                    children.isActive ? 'active' : ''
                  }`}
                >
                  {children.icon && (
                    <children.icon size={17} className="min-w-max" />
                  )}
                  <p className="flex-1 capitalize">{children.title}</p>
                </Link>
              </li>
            );
          } else {
            output = (
              <SubMenu key={i} menu={children} nestedLevel={nestedLevel + 1} />
            );
          }
          return output;
        })}
      </ul>
    </>
  );
};

export { SubMenu };
