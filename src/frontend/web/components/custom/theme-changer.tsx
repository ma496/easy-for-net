import { useDispatch } from 'react-redux'
import { toggleTheme } from '@/store/slices/themeConfigSlice'
import { Sun, Moon, Laptop } from 'lucide-react'
import { cn } from '@/lib/utils'

/**
 * Props for the {@link ThemeChanger} component, providing the current theme and an optional className.
 */
interface ThemeChangerProps {
  theme: string
  className?: string
}

/**
 * Renders a round icon button that cycles through the available theme modes (light, dark, system) and dispatches the new theme to the Redux store.
 */
export const ThemeChanger = ({ theme, className }: ThemeChangerProps) => {
  const dispatch = useDispatch()

  const getThemeIcon = () => {
    switch (theme) {
      case 'light':
        return <Sun />
      case 'dark':
        return <Moon />
      case 'system':
        return <Laptop />
      default:
        return <Sun />
    }
  }

  const getNextTheme = () => {
    switch (theme) {
      case 'light':
        return 'dark'
      case 'dark':
        return 'system'
      case 'system':
        return 'light'
      default:
        return 'light'
    }
  }

  return (
    <button
      className={cn('flex w-9 h-9 cursor-pointer items-center rounded-full bg-white-light/40 p-2 hover:bg-white-light/90 hover:text-primary dark:bg-dark/40 dark:hover:bg-dark/60', className)}
      onClick={() => dispatch(toggleTheme(getNextTheme()))}
    >
      {getThemeIcon()}
    </button>
  )
}

