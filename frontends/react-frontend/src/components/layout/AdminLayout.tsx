import {HierarchicalMenu} from "../HierarchicalMenu";
import {Flex} from "@mantine/core";
import {Outlet} from "react-router-dom";
import {menuItems} from "../../menuItems";

export const AdminLayout: React.FC<{}> = props => {
  return (
    <Flex>
      <div id={'adminMenu'}>
        <HierarchicalMenu items={menuItems} id={'adminMenu'} />
      </div>
      <Outlet />
    </Flex>
  );
};
