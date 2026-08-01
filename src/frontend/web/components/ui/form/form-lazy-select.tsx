'use client'

import { useState, useMemo, useRef, useEffect, useCallback, useId } from 'react'
import { useField, useFormikContext } from 'formik'
import { cn } from '@/lib/utils'
import { ChevronDown, Search, X } from 'lucide-react'
import { TypedUseLazyQuery } from '@reduxjs/toolkit/query/react'
import { ListDto } from '@/store/api'
import { Loading } from '../loading'
import { useDebounce } from '@/hooks/use-debounce'
import { Input } from './input'
import ScrollBar from 'react-perfect-scrollbar'
import { useAppSelector } from '@/store/hooks'

/** Single label/value option used by the lazy-loaded select components. */
interface Option {
  label: string
  value: string
  disabled?: boolean
}

/** Props for the Formik-aware FormLazySelect, a single-select that fetches pages of options on demand via a RTK Query lazy hook and binds the selected value to a form field. */
interface FormLazySelectProps<TItem, TRequest> {
  label?: string
  name: string
  id?: string
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  useLazyQuery: TypedUseLazyQuery<ListDto<TItem>, TRequest, any>
  getLabel: (item: TItem) => string
  getValue: (item: TItem) => string
  isDisabled?: (item: TItem) => boolean
  selectedItemId?: string
  showValidation?: boolean
  className?: string
  icon?: React.ReactNode
  placeholder?: string
  searchable?: boolean
  maxVisibleItems?: number
  disabled?: boolean
  size?: 'default' | 'sm' | 'lg'
  pageSize: number
  generateRequest?: (search: string, page: number, pageSize: number) => TRequest
  required?: boolean
}

/**
 * FormLazySelect is a client component that combines a Formik-bound string field with debounced, server-paginated option fetching, infinite scroll, and the ability to fetch the label of a pre-selected item that is not in the current page.
 */
export const FormLazySelect = <TItem, TRequest>({
  label,
  name,
  id,
  useLazyQuery,
  getLabel,
  getValue,
  isDisabled = () => false,
  selectedItemId,
  showValidation = true,
  className,
  icon,
  placeholder = 'Select...',
  searchable = true,
  maxVisibleItems = 5,
  disabled = false,
  size = 'default',
  pageSize,
  generateRequest,
  required = false,
}: FormLazySelectProps<TItem, TRequest>) => {
  const [field, meta, helpers] = useField(name)
  const { submitCount } = useFormikContext()
  const isDirty = meta.initialValue !== meta.value
  const hasError = (isDirty || submitCount > 0) && meta.error
  const [open, setOpen] = useState(false)
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 500)
  const [page, setPage] = useState(1)
  const [hasMore, setHasMore] = useState(true)
  const [fetchedOptions, setFetchedOptions] = useState<Option[]>([])
  const [isLoadingMore, setIsLoadingMore] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)
  const isRTL = useAppSelector((s) => s.theme.rtlClass) === 'rtl'
  const generatedId = useId()
  const controlId = id ?? generatedId
  const [trigger, { data, isFetching }] = useLazyQuery()
  const [triggerSelected, { data: selectedData }] = useLazyQuery()

  const [storedOptions, setStoredOptions] = useState<Option[]>([])

  const getLabelRef = useRef(getLabel)
  const getValueRef = useRef(getValue)
  const isDisabledRef = useRef(isDisabled)
  const generateRequestRef = useRef(generateRequest)

  useEffect(() => {
    getLabelRef.current = getLabel
    getValueRef.current = getValue
    isDisabledRef.current = isDisabled
    generateRequestRef.current = generateRequest
  }, [getLabel, getValue, isDisabled, generateRequest])

  const allOptions = useMemo(() => {
    const optionsMap = new Map<string, Option>()
    storedOptions.forEach((opt) => optionsMap.set(opt.value, opt))
    fetchedOptions.forEach((opt) => optionsMap.set(opt.value, opt))
    return Array.from(optionsMap.values())
  }, [storedOptions, fetchedOptions])

  useEffect(() => {
    if (selectedItemId && !allOptions.some(opt => opt.value === selectedItemId)) {
      let request: TRequest

      if (generateRequestRef.current) {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        request = generateRequestRef.current('', 1, pageSize) as any
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        (request as any).includeIds = [selectedItemId]
      } else {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const internalRequest: any = {
          page: 1,
          pageSize: 1,
          includeIds: [selectedItemId],
        }
        request = internalRequest as TRequest
      }
      triggerSelected(request)
    }
  }, [selectedItemId, triggerSelected, pageSize, allOptions])

  useEffect(() => {
    if (selectedData?.items && selectedItemId) {
      const selectedItemDetail = selectedData.items.find((item) => getValueRef.current(item) === selectedItemId)
      if (selectedItemDetail) {
        setStoredOptions((prev) => {
          if (prev.some((opt) => opt.value === selectedItemId)) return prev
          return [
            ...prev,
            {
              label: getLabelRef.current(selectedItemDetail),
              value: getValueRef.current(selectedItemDetail),
              disabled: isDisabledRef.current(selectedItemDetail),
            },
          ]
        })
      }
    }
  }, [selectedData, selectedItemId])

  useEffect(() => {
    setPage(1)
    setHasMore(true)
    setFetchedOptions([])
  }, [debouncedSearch])

  useEffect(() => {
    if (hasMore) {
      let request: TRequest

      if (generateRequestRef.current) {
        request = generateRequestRef.current(debouncedSearch, page, pageSize)
      } else {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const internalRequest: any = {
          page,
          pageSize,
        }
        const trimmedSearch = debouncedSearch.trim()
        if (trimmedSearch) {
          internalRequest.search = trimmedSearch
        }
        request = internalRequest as TRequest
      }
      trigger(request)
    }
  }, [debouncedSearch, page, hasMore, pageSize, trigger])

  useEffect(() => {
    if (data?.items) {
      const newOptions = data.items.map((item) => ({
        label: getLabelRef.current(item),
        value: getValueRef.current(item),
        disabled: isDisabledRef.current(item),
      }))

      if (page === 1) {
        setFetchedOptions(newOptions)
      } else {
        setFetchedOptions((prev) => {
          const existingValues = new Set(prev.map((o) => o.value))
          const filteredNewOptions = newOptions.filter((o) => !existingValues.has(o.value))
          return [...prev, ...filteredNewOptions]
        })
      }

      if (data.items.length < pageSize) {
        setHasMore(false)
      }
    }
  }, [data, page, pageSize])

  useEffect(() => {
    if (!isFetching) {
      setIsLoadingMore(false)
    }
  }, [isFetching])


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

  const handleScroll = useCallback(
    (element: HTMLElement) => {
      if (element) {
        const { scrollTop, scrollHeight, clientHeight } = element
        if (scrollHeight - scrollTop <= clientHeight + 20 && hasMore && !isFetching && !isLoadingMore) {
          setIsLoadingMore(true)
          setPage((prevPage) => prevPage + 1)
        }
      }
    },
    [hasMore, isFetching, isLoadingMore],
  )

  const isSelected = (val: string) => field.value === val

  const handleSelect = useCallback(
    (opt: Option) => {
      if (field.value === opt.value) {
        setOpen(false)
        return;
      }

      if (!storedOptions.some((o) => o.value === opt.value)) {
        setStoredOptions((prev) => [...prev, opt])
      }
      helpers.setValue(opt.value).finally(() => helpers.setTouched(true))
      setOpen(false)
    },
    [field.value, helpers, storedOptions],
  )

  const handleClear = useCallback(
    (e: React.MouseEvent) => {
      e.stopPropagation()
      helpers.setValue(undefined)
      setSearch('')
    },
    [helpers],
  )

  const renderValue = () => {
    const selectedOption = allOptions.find((opt) => opt.value === field.value)
    if (selectedOption) {
      return selectedOption.label
    }
    return placeholder || ''
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
            !field.value && 'text-gray-400'
          )
          }
          id={controlId}
          disabled={disabled}
          style={{ backgroundImage: 'none' }}
          onClick={() => setOpen((v) => !v)}
        >
          {icon && <span className="absolute inset-s-4 top-1/2 -translate-y-1/2">{icon}</span>}
          <span className="flex flex-1 items-center truncate">
            {renderValue()}
          </span>
          {field.value && (
            <div role="button" className="absolute inset-e-8 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 focus:outline-hidden" tabIndex={-1} onClick={handleClear}>
              <X size={16} />
            </div>
          )}
          <span className="pointer-events-none absolute inset-e-4 top-1/2 -translate-y-1/2">
            <ChevronDown className={cn('h-4 w-4 transition-transform', open && 'rotate-180')} />
          </span>
        </button>
        <div
          className={cn(
            'absolute left-0 z-50 mt-1 min-w-full overflow-hidden rounded-sm border border-[rgb(224,230,237)] bg-white shadow-lg dark:border-[#253b5c] dark:bg-[#1b2e4b]',
            'custom-select',
            !open && 'hidden',
          )}
          style={{ maxHeight: `${maxVisibleItems * 40 + 8 + (searchable ? 50 : 0)}px` }}
        >
          {searchable && (
            <div className="sticky top-0 z-10 flex items-center border-b border-gray-100 bg-white px-2 py-2 dark:border-[#253b5c] dark:bg-[#1b2e4b]" onClick={(e) => e.stopPropagation()}>
              <Input
                name={`${name}-search`}
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
            options={{
              suppressScrollX: true,
            }}
            style={{
              maxHeight: `${maxVisibleItems * 40}px`,
              direction: isRTL ? 'rtl' : 'ltr',
            }}
            onScrollY={handleScroll}
            key={isRTL ? `${controlId}-rtl` : `${controlId}-ltr`}
          >
            <ul className="overflow-hidden">
              {isFetching && page === 1 && (
                <li className="flex items-center justify-center px-4 py-2 text-gray-400">
                  <Loading />
                </li>
              )}
              {!isFetching && fetchedOptions.length === 0 && <li className="px-4 py-2 text-gray-400">No options</li>}
              {fetchedOptions.map((opt) => (
                <li
                  key={opt.value}
                  className={cn(
                    'flex cursor-pointer items-center gap-2 px-4 py-2 hover:bg-[#f6f6f6] dark:hover:bg-[#132136]',
                    isSelected(opt.value) && 'bg-primary/10 text-primary',
                    opt.disabled && 'pointer-events-none opacity-50',
                  )}
                  onClick={() => handleSelect(opt)}
                >
                  <span className="truncate">{opt.label}</span>
                  {isSelected(opt.value) && <span className="ml-auto">Selected</span>} {/* Optional indicator */}
                </li>
              ))}
              {isFetching && page > 1 && (
                <li className="flex items-center justify-center px-4 py-2 text-gray-400">
                  <Loading />
                </li>
              )}
            </ul>
          </ScrollBar>
        </div>
      </div>
      {showValidation && (isDirty || submitCount > 0) && hasError && <div className="mt-1 text-danger">{meta.error}</div>}
    </div>
  )
}
