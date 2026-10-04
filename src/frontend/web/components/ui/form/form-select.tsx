'use client'

import { useState, useMemo, useRef, useEffect, useId } from 'react'
import { useField, useFormikContext } from 'formik'
import { cn } from '@/lib/utils'
import { useTranslation } from '@/i18n'
import { ChevronDown, Search, X } from 'lucide-react'
import { Portal } from '@headlessui/react'
import { Input } from './input'
import { OptionsScrollArea } from './options-scroll-area'
import { useAppSelector } from '@/store/hooks'
import { useDropdownPosition } from '@/hooks'

/** Single label/value option used by the form select components. */
interface Option {
  label: string
  value: string
  disabled?: boolean
}

/** Props for the Formik-aware FormSelect, a single-select combobox bound to a form field by name with optional search, clear, and icon affordances. */
interface FormSelectProps {
  label?: string
  name: string
  id?: string
  options: Option[]
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
 * FormSelect is a client component that uses Formik's useField/useFormikContext to bind a single-value searchable combobox (popover with options) to a form field, showing validation errors after the field is dirty or the form has been submitted.
 */
export const FormSelect = ({
  label,
  name,
  id,
  options,
  showValidation = true,
  className,
  icon,
  placeholder: placeholderProp,
  searchable = true,
  maxVisibleItems = 5,
  disabled = false,
  size = 'default',
  clearable = true,
  required = false,
}: FormSelectProps) => {
  const { t } = useTranslation()
  const placeholder = placeholderProp ?? t('common.selectPlaceholder')
  const [field, meta, helpers] = useField(name)
  const { submitCount } = useFormikContext()
  const isDirty = meta.initialValue !== meta.value
  const hasError = (isDirty || submitCount > 0) && meta.error
  const [open, setOpen] = useState(false)
  const [search, setSearch] = useState('')
  const containerRef = useRef<HTMLDivElement>(null)
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
      }
    }
    document.addEventListener('mousedown', handleClick)
    return () => document.removeEventListener('mousedown', handleClick)
  }, [open])

  // The panel is drawn in a portal at viewport coordinates, so the height it would like is decided here and the
  // height it actually gets comes back from the positioning hook, capped to the room on the side it opened into.
  const preferredMaxHeight = maxVisibleItems * 40 + 8 + (searchable ? 50 : 0)
  const position = useDropdownPosition(triggerRef, open, preferredMaxHeight)
  const panelMaxHeight = position?.maxHeight ?? preferredMaxHeight
  const listMaxHeight = Math.max(panelMaxHeight - (searchable ? 50 : 0) - 8, 40)

  // Value helpers
  const isSelected = (val: string) => field.value === val

  const handleSelect = (val: string) => {
    helpers.setValue(val, true).finally(() => helpers.setTouched(true))
    setOpen(false)
    setSearch('')
  }

  const handleClear = (e: React.MouseEvent) => {
    e.stopPropagation()
    helpers.setValue('')
    setSearch('')
  }

  // Get selected option label
  const getSelectedLabel = () => {
    if (!field.value) return ''
    const selectedOption = options.find((opt) => opt.value === field.value)
    return selectedOption?.label || ''
  }

  return (
    <div className={cn(className, (isDirty || submitCount > 0) && hasError && 'has-error')} ref={containerRef}>
      {label && (
        <label htmlFor={controlId} className="form-label">
          {label}
          {required && <span className="ms-1 text-danger">*</span>}
        </label>
      )}
      <div className={cn('relative text-muted-foreground', 'custom-select')} ref={triggerRef}>
        <button
          type="button"
          className={cn(
            'form-input flex h-auto min-h-9 w-full cursor-pointer items-center gap-1 bg-transparent pe-10 text-start',
            icon && 'ps-10',
            size === 'sm' && 'min-h-8 py-1 text-[13px]',
            size === 'lg' && 'py-2.5 text-base',
          )}
          id={controlId}
          disabled={disabled}
          style={{ backgroundImage: 'none' }}
          onClick={() => setOpen((v) => !v)}
        >
          {icon && <span className="pointer-events-none absolute inset-s-3 top-1/2 -translate-y-1/2">{icon}</span>}
          <span className={cn('flex-1 truncate ltr:text-left rtl:text-right', !field.value && 'text-subtle-foreground')}>{field.value ? getSelectedLabel() : placeholder}</span>
          {clearable && field.value && (
            <div role="button" className="absolute inset-e-8 top-1/2 -translate-y-1/2 text-subtle-foreground hover:text-muted-foreground focus:outline-hidden" tabIndex={-1} onClick={handleClear}>
              <X size={16} />
            </div>
          )}
          <span className="pointer-events-none absolute inset-e-3 top-1/2 -translate-y-1/2">
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
              className={cn('fixed z-999 overflow-hidden rounded-lg border border-border bg-surface text-foreground shadow-lg', 'custom-select')}
              style={{
                top: position?.bottom === undefined ? `${position?.top ?? 0}px` : undefined,
                bottom: position?.bottom === undefined ? undefined : `${position.bottom}px`,
                left: `${position?.left ?? 0}px`,
                width: `${position?.width ?? 0}px`,
                maxHeight: `${panelMaxHeight}px`,
                zIndex: position?.zIndex,
              }}
            >
              {searchable && (
                <div className="sticky top-0 z-10 flex items-center border-b border-border bg-surface p-1.5" onClick={(e) => e.stopPropagation()}>
                  <Input
                    name={name}
                    id={`${controlId}-search`}
                    type="text"
                    icon={<Search className="pointer-events-none h-4 w-4 text-subtle-foreground" />}
                    placeholder={t('common.search')}
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                    autoFocus
                    showError={false}
                  />
                </div>
              )}
              <OptionsScrollArea maxHeight={listMaxHeight} isRTL={isRTL}>
                <ul>
                  {filteredOptions.length === 0 && <li className="px-3 py-6 text-center text-sm text-subtle-foreground">{t('common.noOptions')}</li>}
                  {filteredOptions.map((opt) => (
                    <li
                      key={opt.value}
                      className={cn(
                        'mx-1 my-0.5 cursor-pointer rounded-md px-3 py-1.5 hover:bg-surface-2',
                        isSelected(opt.value) && 'bg-primary/10 text-primary',
                        opt.disabled && 'pointer-events-none opacity-50',
                      )}
                      onClick={() => handleSelect(opt.value)}
                    >
                      <span>{opt.label}</span>
                    </li>
                  ))}
                </ul>
              </OptionsScrollArea>
            </div>
          </Portal>
        )}
      </div>
      {showValidation && (isDirty || submitCount > 0) && hasError && <div className="mt-1.5 text-xs font-medium text-danger">{meta.error}</div>}
    </div>
  )
}
