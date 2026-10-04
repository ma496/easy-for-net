'use client'

import React, { useId, useRef } from 'react'
import { cva, type VariantProps } from 'class-variance-authority'
import { cn } from '@/lib/utils'
import { useAppSelector } from '@/store/hooks'

const tabVariants = cva('-mb-px inline-flex h-10 cursor-pointer items-center justify-center gap-2 border-b-2 px-3 text-sm font-medium whitespace-nowrap transition-colors disabled:cursor-not-allowed disabled:opacity-50', {
  variants: {
    active: {
      true: 'border-primary text-foreground',
      false: 'border-transparent text-muted-foreground hover:border-border hover:text-foreground',
    },
    // `mobile` shares the row evenly on small screens and sizes each tab to its label from `sm` up.
    fullWidth: {
      mobile: 'flex-1 sm:flex-none',
      always: 'flex-1',
      never: '',
    },
  },
  defaultVariants: {
    active: false,
    fullWidth: 'mobile',
  },
})

/**
 * One entry of the tab strip: the value it selects, what it shows, and whether it can be chosen.
 */
export interface TabItem<T extends string = string> {
  value: T
  label: React.ReactNode
  icon?: React.ReactNode
  disabled?: boolean
}

/**
 * Props for the Tabs component: the entries, the selected value and the change callback (Tabs is
 * always controlled, so the caller decides where the selection lives - state, or the URL), and the
 * panel for the selected entry as `children`.
 */
export interface TabsProps<T extends string = string> extends Omit<React.HTMLAttributes<HTMLDivElement>, 'onChange'>, Pick<VariantProps<typeof tabVariants>, 'fullWidth'> {
  items: readonly TabItem<T>[]
  value: T
  onValueChange: (value: T) => void
  listClassName?: string
  panelClassName?: string
  children?: React.ReactNode
}

/**
 * Tabs renders an accessible tab strip (`tablist`/`tab`/`tabpanel` with roving focus, arrow keys
 * following the reading direction, Home/End) above the panel passed as `children`. Only the selected
 * panel is rendered, so a caller mounts just the content it is showing.
 */
export const Tabs = <T extends string = string>({
  items,
  value,
  onValueChange,
  fullWidth,
  className,
  listClassName,
  panelClassName,
  children,
  ...props
}: TabsProps<T>) => {
  const isRTL = useAppSelector((state) => state.theme.rtlClass) === 'rtl'
  const baseId = useId()
  const tabRefs = useRef<(HTMLButtonElement | null)[]>([])
  const tabId = (candidate: T) => `${baseId}-tab-${candidate}`
  const panelId = `${baseId}-panel`

  const select = (index: number) => {
    const item = items[index]
    if (!item || item.disabled) return
    tabRefs.current[index]?.focus()
    onValueChange(item.value)
  }

  const handleKeyDown = (event: React.KeyboardEvent<HTMLButtonElement>, index: number) => {
    const enabled = items.map((item, i) => (item.disabled ? -1 : i)).filter((i) => i >= 0)
    const position = enabled.indexOf(index)
    if (enabled.length === 0) return

    const next = enabled[(position + 1) % enabled.length]
    const previous = enabled[(position - 1 + enabled.length) % enabled.length]
    const target = {
      ArrowRight: isRTL ? previous : next,
      ArrowLeft: isRTL ? next : previous,
      Home: enabled[0],
      End: enabled[enabled.length - 1],
    }[event.key]

    if (target === undefined) return
    event.preventDefault()
    select(target)
  }

  return (
    <div className={cn('flex flex-col gap-4', className)} {...props}>
      <div role="tablist" className={cn('flex gap-2 overflow-x-auto border-b border-border', listClassName)}>
        {items.map((item, index) => {
          const active = item.value === value
          return (
            <button
              key={item.value}
              ref={(element) => {
                tabRefs.current[index] = element
              }}
              id={tabId(item.value)}
              type="button"
              role="tab"
              aria-selected={active}
              aria-controls={active ? panelId : undefined}
              tabIndex={active ? 0 : -1}
              disabled={item.disabled}
              className={tabVariants({ active, fullWidth })}
              onClick={() => onValueChange(item.value)}
              onKeyDown={(event) => handleKeyDown(event, index)}
            >
              {item.icon && <span className="inline-flex shrink-0 [&_svg]:size-4">{item.icon}</span>}
              {item.label}
            </button>
          )
        })}
      </div>

      <div role="tabpanel" id={panelId} aria-labelledby={tabId(value)} className={panelClassName}>
        {children}
      </div>
    </div>
  )
}

export { tabVariants }
