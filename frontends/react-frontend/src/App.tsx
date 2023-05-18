import React from 'react';
import Sidebar from './components/Sidebar';
import { ISidebarItem } from "./components/Sidebar/SidebarItem";

const items: ISidebarItem[]  = [
  {
    url: '1',
    name: 'Folder 1',
    expanded: true,
    children: [
      {
        url: '1.1',
        name: 'File 1.1',
        expanded: false,
      },
      {
        url: '1.2',
        name: 'File 1.2',
        expanded: false,
      },
    ],
  },
  {
    url: '2',
    name: 'Folder 2',
    expanded: true,
    children: [
      {
        url: '2.1',
        name: 'File 2.1',
        expanded: true,
        children: [
          {
            url: '2.1.1',
            name: 'File 2.1.1',
          },
          {
            url: '2.1.2',
            name: 'File 2.1.2',
          },
        ],
      },
      {
        url: '2.2',
        name: 'File 2.2',
        expanded: false,
      },
    ],
  },
];

const SidebarExample: React.FC = () => {
  return (
    <div>
      <Sidebar items={items} />
    </div>
  );
};

export default SidebarExample;
