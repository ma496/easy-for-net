﻿import {ActionIcon, Burger, Flex, Header, MediaQuery, Menu, Text, useMantineTheme} from "@mantine/core";
import {
  IconArrowsLeftRight,
  IconMessageCircle,
  IconNotification,
  IconPhoto,
  IconSearch,
  IconSettings, IconTrash,
  IconUser
} from "@tabler/icons-react";
import React from "react";

interface AdminHeaderProps {
  opened: boolean
  setOpened:  React.Dispatch<React.SetStateAction<boolean>>
}

export function AdminHeader(props: AdminHeaderProps) {
  const theme = useMantineTheme();

  return (
    <Header height={{base: 50, md: 70}} p="md">
      <Flex align={"center"} style={{height: '100%'}}>
        <Flex>
          <MediaQuery largerThan="sm" styles={{display: 'none'}}>
            <Burger
              opened={props.opened}
              onClick={() => props.setOpened((o) => !o)}
              size="sm"
              color={theme.colors.gray[6]}
              mr="xl"
            />
          </MediaQuery>

          <Text>Application header</Text>
        </Flex>
        <Flex justify={'flex-end'} gap={"xs"} style={{flexGrow: 1}}>
          <ActionIcon variant="light" color={'primary'}><IconNotification size="1rem" /></ActionIcon>
          <Menu shadow="md" width={200}>
            <Menu.Target>
              <ActionIcon variant="light" color={'primary'}><IconUser size="1rem" /></ActionIcon>
            </Menu.Target>

            <Menu.Dropdown>
              <Menu.Label>Application</Menu.Label>
              <Menu.Item icon={<IconSettings size={14} />}>Settings</Menu.Item>
              <Menu.Item icon={<IconMessageCircle size={14} />}>Messages</Menu.Item>
              <Menu.Item icon={<IconPhoto size={14} />}>Gallery</Menu.Item>
              <Menu.Item
                icon={<IconSearch size={14} />}
                rightSection={<Text size="xs" color="dimmed">⌘K</Text>}
              >
                Search
              </Menu.Item>

              <Menu.Divider />

              <Menu.Label>Danger zone</Menu.Label>
              <Menu.Item icon={<IconArrowsLeftRight size={14} />}>Transfer my data</Menu.Item>
              <Menu.Item color="red" icon={<IconTrash size={14} />}>Delete my account</Menu.Item>
            </Menu.Dropdown>
          </Menu>
        </Flex>
      </Flex>
    </Header>
  );
}