import { LightOrDarkTheme } from "../../../theme/light-or-dark-theme"
import { useMainLayoutContext } from "./../context"
import { RiMenuLine, RiMenu2Line } from "react-icons/ri"
import { UserNav } from "./components/user-nav"

const Header = () => {
  const { headerHeight, sidebarOpen, setSidebarOpen, sidebarWidth, getDurationCss } = useMainLayoutContext()

  return (
    <div
      className="bg-background fixed w-full flex shadow-lg gap-4 justify-between items-center pl-2"
      style={{
        ...getDurationCss(),
        height: headerHeight,
        paddingRight: sidebarOpen ? sidebarWidth + 8 : 8
      }}>
      <div>
        {
          !sidebarOpen
            ? (
              <RiMenuLine
                className="cursor-pointer"
                onClick={() => setSidebarOpen(!sidebarOpen)} size={30} />
            )
            : (
              <RiMenu2Line
                className="cursor-pointer"
                onClick={() => setSidebarOpen(!sidebarOpen)} size={30} />
            )
        }
      </div>
      <div className="flex gap-1 items-center">
        <LightOrDarkTheme width={20} height={20} />
        <UserNav width={43} height={43}/>
      </div>
    </div>
  )
}

export { Header }
