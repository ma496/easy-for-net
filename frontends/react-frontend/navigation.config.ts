import { RiProductHuntLine } from "react-icons/ri";
import { BiUser } from "react-icons/bi";
import { AiOutlineSecurityScan, AiOutlineSetting, AiOutlineHome } from "react-icons/ai";
import { RxDashboard } from "react-icons/rx"
import { MenuGroupModel, MenuModel } from "./components/layout/main/models";

const menus: (MenuModel | MenuGroupModel)[] = [
  {
    title: "Home",
    icon: AiOutlineHome,
    url: "/"
  },
  {
    title: "Dashboard",
    icon: RxDashboard,
    url: "/dashboard"
  },
  {
    title: "Products",
    icon: RiProductHuntLine,
    url: "/products"
  },
  {
    group: "Administration",
    menus: [
      {
        title: "Users",
        icon: BiUser,
        url: "/administration/users"
      },
      {
        title: "Roles",
        icon: AiOutlineSecurityScan,
        url: "/administration/roles"
      },
      {
        title: "Settings",
        icon: AiOutlineSetting,
        url: "/administration/settings"
      },
    ]
  },
]

export { menus }
