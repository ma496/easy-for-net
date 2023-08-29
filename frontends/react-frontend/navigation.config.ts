import { RiProductHuntLine } from "react-icons/ri";
import { BiBookmark, BiUser } from "react-icons/bi";
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
  {
    title: "Nested Menu 1",
    icon: BiBookmark,
    url: "/nested-menu-1",
    children: [
      {
        title: "Nested Menu 1.1",
        icon: BiBookmark,
        url: "/1"
      },
      {
        title: "Nested Menu 1.2",
        icon: BiBookmark,
        url: "/2",
        children: [
          {
            title: "Nested Menu 1.2.1",
            icon: BiBookmark,
            url: "/1",
            children: [
              {
                title: "Nested Menu 1.2.1.1",
                icon: BiBookmark,
                url: "/1"
              },
              {
                title: "Nested Menu 1.2.1.2",
                icon: BiBookmark,
                url: "/2"
              },
            ]
          },
          {
            title: "Nested Menu 1.2.2",
            icon: BiBookmark,
            url: "/2"
          },
        ]
      },
      {
        title: "Nested Menu 1.3",
        icon: BiBookmark,
        url: "/3"
      },
    ]
  },
]

export { menus }
