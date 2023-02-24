import React, {SyntheticEvent} from "react";
import {Box, NavLink} from "@mantine/core";
import {MenuItem} from "../menuItems";

function doInactive(id: string) {
  // let itemsElement = document.getElementsByClassName('mantine-NavLink-root');
  let itemsElement = document.querySelectorAll(`#${id} .mantine-NavLink-root`);
  for (let ie of itemsElement) {
    ie.removeAttribute('data-active');
  }
}

export function HierarchicalMenu({items, id}: {items: MenuItem[], id: string}) {

  return (
    <Box>
      {items.map((item) => (
        <NavLink
          key={item.label}
          active={item.active}
          label={item.label}
          description={item.description}
          rightSection={item.rightSection}
          icon={<item.icon size={16} stroke={1.5} />}
          onClick={(e: SyntheticEvent) => {
            doInactive(id);
            if (!e.currentTarget.hasAttribute('data-active')) {
              e.currentTarget.setAttribute('data-active', 'true');
            }
          }}
          defaultOpened={item.opened}>
          {item.children && item.children.length > 0 && (
            <HierarchicalMenu items={item.children} id={id} />
          )}
        </NavLink>
      ))}
    </Box>
  );
}
