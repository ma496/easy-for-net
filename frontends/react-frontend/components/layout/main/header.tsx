import { LightOrDarkTheme } from "../../theme/light-or-dark-theme"
import { useMainLayoutContext } from "./context"
import { RiMenuLine, RiMenu2Line } from "react-icons/ri"

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
      <div>
        <LightOrDarkTheme />
      </div>
    </div>
  )
}

export { Header }
