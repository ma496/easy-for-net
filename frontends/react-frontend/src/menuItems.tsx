import {IconActivity, IconFingerprint, IconGauge} from "@tabler/icons";
import {MenuItem} from "./models/menuItem";

export const menuItems: MenuItem[] = [
  {
    icon: IconGauge,
    label: 'Dashboard',
    description: 'Item with description',
    url: '/'
  },
  {
    icon: IconFingerprint,
    label: 'Security',
    // rightSection: <IconChevronRight size={14} stroke={1.5} />,
    url: '/security'
  },
  {
    icon: IconActivity,
    label: 'Activity',
    opened: true,
    url: '/activity',
    children: [
      {
        icon: IconGauge,
        label: 'Item 1',
        url: '/activity/item-1',
      },
      {
        icon: IconGauge,
        label: 'Item 2',
        url: '/activity/item-2',
      },
      {
        icon: IconGauge,
        label: 'Item 3',
        opened: true,
        children: [
          {
            icon: IconGauge,
            label: 'Item 1',
            url: '/activity/item-3/item-1',
          },
          {
            icon: IconGauge,
            label: 'Item 2'
          },
          {
            icon: IconGauge,
            label: 'Item 3'
          }
        ]
      }
    ]
  },
];
