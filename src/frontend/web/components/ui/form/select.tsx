'use client'

import { useState, useMemo, useRef, useEffect, useId } from 'react'
import { cn } from '@/lib/utils'
import { ChevronDown, Search, X } from 'lucide-react'
import ScrollBar from 'react-perfect-scrollbar'
import { Portal } from '@headlessui/react'
import { Input } from './input'
import { useAppSelector } from '@/store/hooks'
import { useDropdownPosition } from '@/hooks'

/** Single label/value option used by the Select component. */
interface Option {
  label: string
  value: string
  disabled?: boolean
}

/** Props for the standalone Select, a controlled single-value combobox that takes a name-keyed onChange/onTouch pair and accepts touched/error from a parent (typically Formik via custom wiring). */
interface SelectProps {
  label?: string
  name: string // Kept for the 'onChange' handler and <label> 'for'
  id?: string
  options: Option[]
  value: string // Controlled component value
  onChange: (name: string, value: string) => void // Standard change handler
  onTouch?: (name: string) => void // Optional touch handler (for validation)
  error?: string | null // Validation error from parent
  touched?: boolean // Touched state from parent
  showValidation?: boolean
  className?: string
  icon?: React.ReactNode
  placeholder?: string
  searchable?: boolean
  maxVisibleItems?: number
  disabled?: boolean
  size?: 'default' | 'sm' | 'lg'
  clearable?: boolean
  required?: boolean
}

/**
 * Select is a client component that renders a fully controlled single-value searchable combobox popover, designed to be wired up to a parent form (typically via Formik) using the name-keyed onChange/onTouch callbacks.
 */
export const Select = ({
  label,
  name,
  id,
  options,
  value,
  onChange,
  onTouch,
  error,
  touched,
  showValidation = true,
  className,
  icon,
  placeholder = 'Select...',
  searchable = true,
  maxVisibleItems = 5,
  disabled = false,
  size = 'default',
  clearable = true,
  required = false,
}: SelectProps) => {
  const hasError = touched && error
  const [open, setOpen] = useState(false)
  const [search, setSearch] = useState('')
  const containerRef = useRef<HTMLDivElement>(null!)
  const triggerRef = useRef<HTMLDivElement>(null)
  const dropdownRef = useRef<HTMLDivElement>(null)
  const isRTL = useAppSelector((s) => s.theme.rtlClass) === 'rtl'
  const generatedId = useId()
  const controlId = id ?? generatedId

  // Filtered options
  const filteredOptions = useMemo(() => {
    if (!searchable || !search) return options
    return options.filter((opt) => opt.label.toLowerCase().includes(search.toLowerCase()))
  }, [search, options, searchable])

  // Handle outside click
  useEffect(() => {
    if (!open) return
    const handleClick = (e: MouseEvent) => {
      // The panel is portalled out of the field, so it has to be asked as well before a click counts as "outside".
      if (!containerRef.current?.contains(e.target as Node) && !dropdownRef.current?.contains(e.target as Node)) {
        setOpen(false)
        onTouch?.(name) // Trigger touch on blur (click-outside)
      }
    }
    document.addEventListener('mousedown', handleClick)
    return () => document.removeEventListener('mousedown', handleClick)
  }, [open, name, onTouch])

  // Value helpers
  const isSelected = (val: string) => value === val

  const handleSelect = (val: string) => {
    onChange(name, val) // Call parent onChange
    onTouch?.(name) // Call parent onTouch
    setOpen(false)
    setSearch('')
  }

  const handleClear = (e: React.MouseEvent) => {
    e.stopPropagation()
    onChange(name, '') // Call parent onChange with empty value
    onTouch?.(name) // Call parent onTouch
    setSearch('')
  }

  // The panel is drawn in a portal at viewport coordinates, so the height it would like is decided here and the
  // height it actually gets comes back from the positioning hook, capped to the room on the side it opened into.
  const preferredMaxHeight = maxVisibleItems * 40 + 8 + (searchable ? 50 : 0)
  const position = useDropdownPosition(triggerRef, open, preferredMaxHeight)
  const panelMaxHeight = position?.maxHeight ?? preferredMaxHeight
  const listMaxHeight = Math.max(panelMaxHeight - (searchable ? 50 : 0) - 8, 40)

  // Get selected option label
  const getSelectedLabel = () => {
    if (!value) return ''
    const selectedOption = options.find((opt) => opt.value === value)
    return selectedOption?.label || ''
  }

  return (
    <div className={cn(className, hasError && 'has-error')} ref={containerRef}>
      {label && (
        <label htmlFor={controlId}>
          {label}
          {required && <span className="ms-1 text-danger">*</span>}
        </label>
      )}
      <div className={cn('relative text-white-dark', 'custom-select')} ref={triggerRef}>
        <button
          type="button"
          className={cn(
            'form-input flex min-h-10 w-full cursor-pointer items-center gap-1 bg-transparent pe-10 ltr:text-left rtl:text-right',
            icon && 'ps-10',
            size === 'sm' && 'py-1.5 text-xs',
            size === 'lg' && 'py-2.5 text-base',
          )
          }
          id={controlId}
          disabled={disabled}
          style={{ backgroundImage: 'none' }}
          onClick={() => setOpen((v) => !v)}
        >
          {icon && <span className="absolute inset-s-4 top-1/2 -translate-y-1/2">{icon}</span>}
          <span className={cn('flex-1 truncate ltr:text-left rtl:text-right', !value && 'text-gray-400')}>{value ? getSelectedLabel() : placeholder}</span>
          {clearable && value && (
            <div role="button" className="absolute inset-e-8 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 focus:outline-hidden" tabIndex={-1} onClick={handleClear}>
              <X size={16} />
            </div>
          )}
          <span className="pointer-events-none absolute inset-e-4 top-1/2 -translate-y-1/2">
            <ChevronDown className={cn('h-4 w-4 transition-transform', open && 'rotate-180')} />
          </span>
        </button>
        {/* Rendered through a portal so a container that clips its overflow - a modal panel, a card, a scrolling table -
            cannot cut the panel off; inside a headless-ui Dialog the portal is registered as part of the dialog, so
            clicking the panel neither closes the modal nor fights its focus trap. */}
        {open && (
          <Portal>
            <div
              ref={dropdownRef}
              className={cn('fixed z-999 overflow-hidden rounded-sm border border-[rgb(224,230,237)] bg-white text-white-dark shadow-lg dark:border-[#253b5c] dark:bg-[#1b2e4b]', 'custom-select')}
              style={{
                top: `${position?.top ?? 0}px`,
                left: `${position?.left ?? 0}px`,
                width: `${position?.width ?? 0}px`,
                maxHeight: `${panelMaxHeight}px`,
              }}
            >
              {searchable && (
                <div className="sticky top-0 z-10 flex h-12.5 items-center border-b border-gray-100 bg-white px-2 py-2 dark:border-[#253b5c] dark:bg-[#1b2e4b]" onClick={(e) => e.stopPropagation()}>
                  <Input
                    name={name}
                    id={`${controlId}-search`}
                    type="text"
                    icon={<Search className="pointer-events-none h-4 w-4 text-gray-400" />}
                    placeholder="Search..."
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                    autoFocus
                    showError={false}
                  />
                </div>
              )}
              <ScrollBar
                options={{ suppressScrollX: true }}
                style={{
                  maxHeight: `${listMaxHeight}px`,
                  direction: isRTL ? 'rtl' : 'ltr',
                }}
                key={isRTL ? `${controlId}-rtl` : `${controlId}-ltr`}
              >
                <ul>
                  {filteredOptions.length === 0 && <li className="px-4 py-2 text-gray-400">No options</li>}
                  {filteredOptions.map((opt) => (
                    <li
                      key={opt.value}
                      className={cn(
                        'cursor-pointer px-4 py-2 hover:bg-[#f6f6f6] dark:hover:bg-[#132136]',
                        isSelected(opt.value) && 'bg-primary/10 text-primary',
                        opt.disabled && 'pointer-events-none opacity-50',
                      )}
                      onClick={() => handleSelect(opt.value)}
                    >
                      <span>{opt.label}</span>
                    </li>
                  ))}
                </ul>
              </ScrollBar>
            </div>
          </Portal>
        )}
      </div>
      {showValidation && hasError && <div className="mt-1 text-danger">{error}</div>}
    </div>
  )
}
