import { IconType } from 'react-icons';

export type MenuModel = {
  title: string
  url: string
  icon?: IconType
  children?: MenuModel[]
}

export type MenuGroupModel = {
  group: string
  menus: MenuModel[]
}
