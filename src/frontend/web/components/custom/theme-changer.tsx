'use client'
import { useRef } from 'react'
import { useDispatch } from 'react-redux'
import { toggleTheme } from '@/store/slices'
import { Sun, Moon, Laptop, Check } from 'lucide-react'
import { Dropdown, type DropdownRef } from '@/components/ui'
import { useAppSelector } from '@/store/hooks'
import { useTranslation } from '@/i18n'
import { cn } from '@/lib/utils'

/**
 * Props for the {@link ThemeChanger} component, providing the current theme and an optional className.
 */
interface ThemeChangerProps {
  theme: string
  className?: string
}

const options = [
  { value: 'light', icon: Sun, label: 'common.themeLight' },
  { value: 'dark', icon: Moon, label: 'common.themeDark' },
  { value: 'system', icon: Laptop, label: 'common.themeSystem' },
] as const

/**
 * Icon button showing the active color scheme that opens a menu to pick light, dark, or the operating system's, dispatching the choice to the Redux store.
 */
export const ThemeChanger = ({ theme, className }: ThemeChangerProps) => {
  const dispatch = useDispatch()
  const { t } = useTranslation()
  const dropdownRef = useRef<DropdownRef>(null)
  const isRtl = useAppSelector((state) => state.theme.rtlClass) === 'rtl'
  const current = options.find((option) => option.value === theme) ?? options[2]

  return (
    <Dropdown
      ref={dropdownRef}
      placement={isRtl ? 'bottom-start' : 'bottom-end'}
      btnClassName={cn('icon-btn', className)}
      button={
        <span title={t('common.theme')} aria-label={t('common.theme')}>
          <current.icon size={18} />
        </span>
      }
    >
      <ul className="w-40">
        {options.map((option) => (
          <li key={option.value}>
            <button
              type="button"
              className={cn(theme === option.value && 'text-primary')}
              onClick={() => {
                dispatch(toggleTheme(option.value))
                dropdownRef.current?.close()
              }}
            >
              <option.icon size={16} className="shrink-0" />
              <span className="flex-1">{t(option.label)}</span>
              {theme === option.value && <Check size={14} className="shrink-0" />}
            </button>
          </li>
        ))}
      </ul>
    </Dropdown>
  )
}
