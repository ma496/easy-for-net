export interface MenuItem {
  icon: any,
  label: string,
  description?: string,
  children?: MenuItem[],
  rightSection?: JSX.Element,
  opened?: boolean,
  active?: boolean
  url?: string
}
