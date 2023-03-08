import React, {useState} from 'react';
import {
  ActionIcon,
  AppShell,
  Burger, Button,
  Flex,
  Header,
  MediaQuery,
  Menu,
  Navbar,
  Text,
  useMantineTheme,
} from '@mantine/core';
import {Outlet} from "react-router-dom";
import {menuItems} from "../../../menuItems";
import {HierarchicalMenu} from "../../HierarchicalMenu";
import {
  IconArrowsLeftRight,
  IconMessageCircle,
  IconNotification,
  IconPhoto,
  IconSearch,
  IconSettings, IconTrash,
  IconUser
} from "@tabler/icons";
import {AdminHeader} from "./AdminHeader";

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
        <Navbar p="md" hiddenBreakpoint="sm" hidden={!opened} width={{sm: 200, lg: 300}}>
          <HierarchicalMenu items={menuItems} id={'adminMenu'}/>
        </Navbar>
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
        // <Header height={{base: 50, md: 70}} p="md">
        //   <Flex align={"center"} style={{height: '100%'}}>
        //     <Flex>
        //       <MediaQuery largerThan="sm" styles={{display: 'none'}}>
        //         <Burger
        //           opened={opened}
        //           onClick={() => setOpened((o) => !o)}
        //           size="sm"
        //           color={theme.colors.gray[6]}
        //           mr="xl"
        //         />
        //       </MediaQuery>
        //
        //       <Text>Application header</Text>
        //     </Flex>
        //     <Flex justify={'flex-end'} gap={"xs"} style={{flexGrow: 1}}>
        //       <ActionIcon variant="light" color={'primary'}><IconNotification size="1rem" /></ActionIcon>
        //       <Menu shadow="md" width={200}>
        //         <Menu.Target>
        //           <ActionIcon variant="light" color={'primary'}><IconUser size="1rem" /></ActionIcon>
        //         </Menu.Target>
        //
        //         <Menu.Dropdown>
        //           <Menu.Label>Application</Menu.Label>
        //           <Menu.Item icon={<IconSettings size={14} />}>Settings</Menu.Item>
        //           <Menu.Item icon={<IconMessageCircle size={14} />}>Messages</Menu.Item>
        //           <Menu.Item icon={<IconPhoto size={14} />}>Gallery</Menu.Item>
        //           <Menu.Item
        //             icon={<IconSearch size={14} />}
        //             rightSection={<Text size="xs" color="dimmed">⌘K</Text>}
        //           >
        //             Search
        //           </Menu.Item>
        //
        //           <Menu.Divider />
        //
        //           <Menu.Label>Danger zone</Menu.Label>
        //           <Menu.Item icon={<IconArrowsLeftRight size={14} />}>Transfer my data</Menu.Item>
        //           <Menu.Item color="red" icon={<IconTrash size={14} />}>Delete my account</Menu.Item>
        //         </Menu.Dropdown>
        //       </Menu>
        //     </Flex>
        //   </Flex>
        // </Header>
      }
    >
      <Outlet />
    </AppShell>
  );
}
