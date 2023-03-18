import React, {useState} from 'react';
import {AppShell, useMantineTheme,} from '@mantine/core';
import {Outlet} from "react-router-dom";
import {AdminHeader} from "./AdminHeader";
import {AdminNavbar} from "./AdminNavbar";
import {NavbarDemo} from "./NavbarDemo";

export function AdminLayout() {
  const theme = useMantineTheme();
  const [opened, setOpened] = useState(false);
  return (
    <AppShell
      styles={{
        main: {
          background: theme.colorScheme === 'dark' ? theme.colors.dark[8] : theme.colors.gray[0],
        },
      }}
      navbarOffsetBreakpoint="sm"
      asideOffsetBreakpoint="sm"
      navbar={
        <AdminNavbar opened={opened} />
        // <NavbarDemo />
      }
      // aside={
      //   <MediaQuery smallerThan="sm" styles={{display: 'none'}}>
      //     <Aside p="md" hiddenBreakpoint="sm" width={{sm: 200, lg: 300}}>
      //       <Text>Application sidebar</Text>
      //     </Aside>
      //   </MediaQuery>
      // }
      // footer={
      //   <Footer height={60} p="md">
      //     Application footer
      //   </Footer>
      // }
      header={
        <AdminHeader opened={opened} setOpened={setOpened} />
      }
    >
      <Outlet />
    </AppShell>
  );
}
