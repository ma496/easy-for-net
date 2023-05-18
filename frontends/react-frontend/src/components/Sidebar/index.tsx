import React from 'react';
import { ISidebarItem } from './SidebarItem';
import SidebarItem from './SidebarItem';

interface SidebarProps {
  items: ISidebarItem[];
}

const Sidebar: React.FC<SidebarProps> = ({ items }) => {
  return (
    <div className="w-64 h-screen bg-gray-200">
      {items.map((item) => (
        <SidebarItem key={item.url} item={item} />
      ))}
    </div>
  );
};

export default Sidebar;
