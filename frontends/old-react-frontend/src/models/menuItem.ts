import {TablerIcon} from "@tabler/icons";

export interface MenuItem {
  icon: TablerIcon,
  label: string,
  description?: string,
  children?: MenuItem[],
  rightSection?: JSX.Element,
  opened?: boolean,
  active?: boolean
  url?: string
}
