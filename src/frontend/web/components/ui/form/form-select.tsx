'use client'

import { useState, useMemo, useRef, useEffect, useId } from 'react'
import { useField, useFormikContext } from 'formik'
import { cn } from '@/lib/utils'
import { useTranslation } from '@/i18n'
import { ChevronDown, Search, X } from 'lucide-react'
import { Input } from './input'
import { OptionsScrollArea } from './options-scroll-area'
import { useAppSelector } from '@/store/hooks'

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
      <div className={cn('relative text-muted-foreground', 'custom-select')}>
        <button
          type="button"
          className={cn(
            'form-input flex h-auto min-h-9 w-full cursor-pointer items-center gap-1 bg-transparent pe-10 text-start',
            icon && 'ps-10',
            size === 'sm' && 'min-h-8 py-1 text-[13px]',
            size === 'lg' && 'py-2.5 text-base',
          )
          }
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
        {open && (
          <div
            className={cn('absolute start-0 z-50 mt-1 min-w-full rounded-lg border border-border bg-surface text-foreground shadow-lg', 'custom-select')}
            style={{ maxHeight: `${maxVisibleItems * 40 + 8 + (searchable ? 50 : 0)}px` }}
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
            <OptionsScrollArea maxHeight={maxVisibleItems * 40} isRTL={isRTL}>
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
        )}
      </div>
      {showValidation && (isDirty || submitCount > 0) && hasError && <div className="mt-1.5 text-xs font-medium text-danger">{meta.error}</div>}
    </div>
  )
}
