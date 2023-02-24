import {IconActivity, IconFingerprint, IconGauge, TablerIcon} from "@tabler/icons";

export interface MenuItem {
  icon: TablerIcon,
  label: string,
  description?: string,
  children?: MenuItem[],
  rightSection?: JSX.Element,
  opened?: boolean,
  active?: boolean
}

export const menuItems: MenuItem[] = [
  {
    icon: IconGauge,
    label: 'Dashboard',
    description: 'Item with description',
    active: true
  },
  {
    icon: IconFingerprint,
    label: 'Security',
    // rightSection: <IconChevronRight size={14} stroke={1.5} />,
  },
  { icon: IconActivity, label: 'Activity', opened: true,
    children: [
      {
        icon: IconGauge,
        label: 'Item 1'
      },
      {
        icon: IconGauge,
        label: 'Item 2'
      },
      {
        icon: IconGauge,
        label: 'Item 3',
        opened: true,
        children: [
          {
            icon: IconGauge,
            label: 'Item 1'
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
