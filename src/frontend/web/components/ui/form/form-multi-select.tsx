'use client'

import { useState, useMemo, useRef, useEffect, useId } from 'react'
import { useField, useFormikContext } from 'formik'
import { cn } from '@/lib/utils'
import { ChevronDown, Search, X } from 'lucide-react'
import ScrollBar from 'react-perfect-scrollbar'
import { Input } from './input'
import { useAppSelector } from '@/store/hooks'

/** Single label/value option used by the multi-select components. */
interface Option {
  label: string
  value: string
  disabled?: boolean
}

/** Props for the Formik-aware FormMultiSelect, a multi-select combobox bound to an array form field by name with optional search, icon, and size variants. */
interface FormMultiSelectProps {
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
  required?: boolean
}

/**
 * FormMultiSelect is a client component that uses Formik's useField/useFormikContext to bind a multi-value, badge-rendered combobox to a form field, showing validation errors after the field is dirty or the form has been submitted.
 */
export const FormMultiSelect = ({
  label,
  name,
  id,
  options,
  showValidation = true,
  className,
  icon,
  placeholder = 'Select...',
  searchable = true,
  maxVisibleItems = 5,
  disabled = false,
  size = 'default',
  required = false,
}: FormMultiSelectProps) => {
  const [field, meta, helpers] = useField(name)
  const { submitCount } = useFormikContext()
  const isDirty = (Array.isArray(meta.initialValue) && Array.isArray(meta.value))
    ? JSON.stringify(meta.initialValue) !== JSON.stringify(meta.value)
    : meta.initialValue !== meta.value
  const hasError = (isDirty || submitCount > 0) && meta.error
  const [open, setOpen] = useState(false)
  const [search, setSearch] = useState('')
  const containerRef = useRef<HTMLDivElement>(null)
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
      if (!containerRef.current?.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClick)
    return () => document.removeEventListener('mousedown', handleClick)
  }, [open])

  // Value helpers
  const isSelected = (val: string) => (Array.isArray(field.value) ? field.value.includes(val) : false)

  const handleSelect = (val: string) => {
    let newValue = Array.isArray(field.value) ? [...field.value] : []
    if (newValue.includes(val)) {
      newValue = newValue.filter((v) => v !== val)
    } else {
      newValue.push(val)
    }
    helpers.setValue(newValue).finally(() => helpers.setTouched(true))
    // With a single option there is nothing else to pick, so the panel closes instead of waiting for an outside click.
    if (options.length === 1) setOpen(false)
  }

  const handleClear = (e: React.MouseEvent) => {
    e.stopPropagation()
    helpers.setValue([])
    setSearch('')
  }

  // Render selected label(s) as badges
  const renderValue = () => {
    if (Array.isArray(field.value) && field.value.length > 0) {
      return (
        <div className="flex flex-wrap items-center gap-1">
          {options
            .filter((opt) => field.value.includes(opt.value))
            .map((opt) => (
              <span key={opt.value} className="badge flex items-center gap-1 badge-outline-secondary pr-1">
                {opt.label}
                <div
                  role="button"
                  className="ml-1 text-xs text-primary hover:text-danger focus:outline-hidden"
                  tabIndex={-1}
                  onClick={(e) => {
                    e.stopPropagation()
                    handleSelect(opt.value)
                  }}
                >
                  <X size={12} />
                </div>
              </span>
            ))}
        </div>
      )
    }
    return <div className="flex flex-wrap items-center gap-1">&nbsp;</div>
  }

  return (
    <div className={cn(className, (isDirty || submitCount > 0) && hasError && 'has-error')} ref={containerRef}>
      {label && (
        <label htmlFor={controlId}>
          {label}
          {required && <span className="ms-1 text-danger">*</span>}
        </label>
      )}
      <div className={cn('relative text-white-dark', 'custom-select')}>
        <button
          type="button"
          className={cn(
            'form-input flex min-h-10 w-full cursor-pointer flex-wrap items-center gap-1 bg-transparent py-0.5 pr-10 text-left',
            icon && 'ps-10',
            size === 'sm' && 'py-1 text-xs',
            size === 'lg' && 'py-1.75 text-base',
          )
          }
          id={controlId}
          disabled={disabled}
          style={{ backgroundImage: 'none' }}
          onClick={() => setOpen((v) => !v)}
        >
          {icon && <span className="absolute inset-s-4 top-1/2 -translate-y-1/2">{icon}</span>}
          <span className={cn('flex flex-1 flex-wrap items-center gap-1', !field.value?.length && 'text-gray-400')}>
            {Array.isArray(field.value) && field.value.length > 0 ? renderValue() : placeholder && (!field.value || field.value.length === 0) && placeholder}
          </span>
          {Array.isArray(field.value) && field.value.length > 0 && (
            <div role="button" className="absolute inset-e-8 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 focus:outline-hidden" tabIndex={-1} onClick={handleClear}>
              <X size={16} />
            </div>
          )}
          <span className="pointer-events-none absolute inset-e-4 top-1/2 -translate-y-1/2">
            <ChevronDown className={cn('h-4 w-4 transition-transform', open && 'rotate-180')} />
          </span>
        </button>
        {open && (
          <div
            className={cn(
              'absolute left-0 z-50 mt-1 min-w-full overflow-hidden rounded-sm border border-[rgb(224,230,237)] bg-white shadow-lg dark:border-[#253b5c] dark:bg-[#1b2e4b]',
              'custom-select',
            )}
            style={{ maxHeight: `${maxVisibleItems * 40 + 8 + (searchable ? 50 : 0)}px` }}
          >
            {searchable && (
              <div className="sticky top-0 z-10 flex items-center border-b border-gray-100 bg-white px-2 py-2 dark:border-[#253b5c] dark:bg-[#1b2e4b]" onClick={(e) => e.stopPropagation()}>
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
                maxHeight: `${maxVisibleItems * 40}px`,
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
                      'flex cursor-pointer items-center gap-2 px-4 py-2 hover:bg-[#f6f6f6] dark:hover:bg-[#132136]',
                      isSelected(opt.value) && 'bg-primary/10 text-primary',
                      opt.disabled && 'pointer-events-none opacity-50',
                    )}
                    onClick={() => handleSelect(opt.value)}
                  >
                    {isSelected(opt.value) && <input type="checkbox" checked={isSelected(opt.value)} readOnly className="form-checkbox h-4 w-4 text-primary" />}
                    <span>{opt.label}</span>
                  </li>
                ))}
              </ul>
            </ScrollBar>
          </div>
        )}
      </div>
      {showValidation && (isDirty || submitCount > 0) && hasError && <div className="mt-1 text-danger">{meta.error}</div>}
    </div>
  )
}
