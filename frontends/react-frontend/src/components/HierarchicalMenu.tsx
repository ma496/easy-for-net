import React, {SyntheticEvent, useEffect} from "react";
import {Box, NavLink} from "@mantine/core";
import {useLocation, useNavigate} from "react-router-dom";
import {MenuItem} from "../models/menuItem";

function doInactive(id: string) {
  let itemsElement = document.querySelectorAll(`#${id} .mantine-NavLink-root`);
  for (let ie of itemsElement) {
    ie.removeAttribute('data-active');
  }
}

function HierarchicalMenuInternal({items, id}: {items: MenuItem[], id: string}) {
  const navigate = useNavigate();

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
          data-href={item.url}
          onClick={(_: SyntheticEvent) => {
            if (item.url) {
              navigate(item.url);
            }
          }}
          defaultOpened={item.opened}>
          {item.children && item.children.length > 0 && (
            <HierarchicalMenuInternal items={item.children} id={id} />
          )}
        </NavLink>
      ))}
    </Box>
  );
}

export function HierarchicalMenu({items, id}: {items: MenuItem[], id: string}) {
  const location = useLocation();
  useEffect(() => {
    doInactive(id);
    let elements = document.querySelectorAll(`#${id} [data-href="${location.pathname}"]`);
    if (elements && elements.length > 0) {
      let element = elements[0];
      if (!element.hasAttribute('data-active')) {
        element.setAttribute('data-active', 'true');
      }
    }
  }, [location]);

  return (
    <div id={id}>
      <HierarchicalMenuInternal items={items} id={id} />
    </div>
  );
}
