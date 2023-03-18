import {HierarchicalMenu} from "../../HierarchicalMenu";
import {menuItems} from "../../../menuItems";
import {Navbar} from "@mantine/core";
import React from "react";

interface IAdminNavbarProps {
  opened: boolean
}

export function AdminNavbar(props: IAdminNavbarProps) {
  return (
    <Navbar p="md" hiddenBreakpoint="sm" hidden={!props.opened} width={{sm: 200, lg: 300}}
            variant={'pink'}>
      <HierarchicalMenu items={menuItems} id={'adminMenu'}/>
    </Navbar>
  );
}
