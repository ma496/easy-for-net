import React, { ReactNode } from 'react';
import Sidebar from '../ui/sidebar';

type RootLayoutProps = {
  children: ReactNode;
};

const MainLayout: React.FC<RootLayoutProps> = ({ children }) => {
  return (
    <div className="flex gap-5">
      <Sidebar />
      <main className="max-w-5xl flex-1 mx-auto py-4">{children}</main>
    </div>
  );
};

export default MainLayout
