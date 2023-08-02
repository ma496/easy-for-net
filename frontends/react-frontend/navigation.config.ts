import { AiOutlineAppstore } from "react-icons/ai";
import { MenuModel } from "./components/ui/sidebar/menuModel";
import { BsPerson } from "react-icons/bs";
import { HiOutlineDatabase } from "react-icons/hi";
import { RiBuilding3Line } from "react-icons/ri";
import { TbReportAnalytics } from "react-icons/tb";
import { SlSettings } from "react-icons/sl";

const menus: MenuModel[] = [
  {
    title: "All Apps",
    icon: AiOutlineAppstore,
    url: "/"
  },
  {
    title: "Authentication",
    icon: BsPerson,
    url: "/authentication"
  },
  {
    title: "Storage",
    icon: HiOutlineDatabase,
    url: "/storage"
  },
  {
    title: "build",
    icon: RiBuilding3Line,
    url: "build",
    children: [
      { title: "auth", url: "auth" },
      { title: "app settings", url: "app-settings" },
      { title: "storage", url: "storage" },
      { title: "hosting", url: "hosting" }
    ],
  },
  {
    title: "analytics",
    icon: TbReportAnalytics,
    url: "analytics",
    children: [
      { title: "dashboard", url: "dashboard" },
      { title: "realtime", url: "realtime" },
      { title: "events", url: "events" },
    ]
  },
  {
    title: "Settings",
    icon: SlSettings,
    url: "/settings"
  },
]

export {menus}
