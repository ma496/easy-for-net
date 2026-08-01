import { forwardRef, useImperativeHandle, useState, useRef, useEffect, ReactNode } from 'react'
import { cn } from '@/lib/utils'

/**
 * Props for the {@link Dropdown} component, controlling placement, trigger content, and disabled state.
 */
interface DropdownProps {
  placement?: 'top-start' | 'top-end' | 'bottom-start' | 'bottom-end' | 'right-start' | 'right-end' | 'left-start' | 'left-end'
  button: ReactNode
  children: ReactNode
  btnClassName?: string
  isDisabled?: boolean
}

/**
 * Imperative ref handle for the {@link Dropdown} component, exposing a `close` method to programmatically dismiss the menu.
 */
export interface DropdownRef {
  close: () => void
}

/**
 * Interactive dropdown menu that toggles its content visibility on button click and closes when clicking outside.
 * Forwards a ref exposing a `close` method to dismiss the dropdown from parent components.
 */
const Dropdown = forwardRef<DropdownRef, DropdownProps>((props, ref) => {
  const [visibility, setVisibility] = useState(false)
  const dropdownRef = useRef<HTMLDivElement>(null)

  useImperativeHandle(ref, () => ({
    close() {
      setVisibility(false)
    },
  }))

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setVisibility(false)
      }
    }

    document.addEventListener('mousedown', handleClickOutside)
    return () => {
      document.removeEventListener('mousedown', handleClickOutside)
    }
  }, [])

  const getDropdownPosition = () => {
    switch (props.placement) {
      case 'top-start':
        return 'bottom-full left-0'
      case 'top-end':
        return 'bottom-full right-0'
      case 'bottom-end':
        return 'top-full right-0'
      case 'right-start':
        return 'left-full top-0'
      case 'right-end':
        return 'left-full bottom-0'
      case 'left-start':
        return 'right-full top-0'
      case 'left-end':
        return 'right-full bottom-0'
      case 'bottom-start':
      default:
        return 'top-full left-0'
    }
  }

  return (
    <div className="relative inline-block" ref={dropdownRef}>
      <button type="button" className={cn('cursor-pointer', props.btnClassName)} onClick={() => setVisibility(!visibility)} disabled={props.isDisabled}>
        {props.button}
      </button>
      {visibility && (
        <div className={`absolute z-10 ${getDropdownPosition()}`}>
          <div className="rounded-md bg-white dark:bg-gray-800 dark:text-white">{props.children}</div>
        </div>
      )}
    </div>
  )
})

Dropdown.displayName = 'Dropdown'

export default Dropdown
