import React from "react";
import { IconType } from "react-icons";
import { BsFolder } from "react-icons/bs";
import { FiChevronDown, FiChevronUp } from "react-icons/fi";

export interface ISidebarItem {
  name: string;
  url: string;
  icon?: IconType;
  expanded?: boolean;
  children?: ISidebarItem[];
}

interface SidebarItemProps {
  item: ISidebarItem;
}

const SidebarItem: React.FC<SidebarItemProps> = ({ item }) => {
  const [isExpanded, setIsExpanded] = React.useState(item.expanded ?? false);

  const toggleExpand = () => {
    setIsExpanded(!isExpanded);
  };

  const Icon: IconType = item.icon ?? BsFolder;

  return (
    <div>
      <div
        className="flex items-center cursor-pointer sidebar-item hover:bg-gray-300 p-2 rounded"
        onClick={toggleExpand}
      >
        <Icon className="mr-2" />
        {item.name}
        {item.children && (
          <div className="ml-auto">
            {isExpanded ? <FiChevronUp /> : <FiChevronDown />}
          </div>
        )}
      </div>
      {isExpanded && item.children && (
        <div className="pl-4">
          {item.children.map((child) => (
            <SidebarItem key={child.url} item={child} />
          ))}
        </div>
      )}
    </div>
  );
};

export default SidebarItem;
