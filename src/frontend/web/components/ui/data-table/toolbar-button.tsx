'use client'

import { ButtonHTMLAttributes, forwardRef, ReactNode } from 'react'
import { cva } from 'class-variance-authority'
import { cn } from '@/lib/utils'
import { Tooltip } from '../tooltip'
import { LocalizedLink } from '../localized-link'

const toolbarButtonVariants = cva(
  'relative inline-flex size-9.5 shrink-0 cursor-pointer items-center justify-center rounded-md border shadow-xs transition-colors duration-200 focus-visible:ring-2 focus-visible:ring-primary/30 focus-visible:outline-hidden disabled:cursor-not-allowed disabled:opacity-50',
  {
    variants: {
      active: {
        false:
          'border-white-light bg-white text-gray-500 not-disabled:hover:border-primary/40 not-disabled:hover:bg-primary/5 not-disabled:hover:text-primary dark:border-[#17263c] dark:bg-[#121e32] dark:text-white-dark dark:not-disabled:hover:text-primary',
        true: 'border-primary/40 bg-primary/5 text-primary dark:bg-primary/10',
      },
    },
    defaultVariants: {
      active: false,
    },
  },
)

/** Props for DataTableToolbarButton, an icon-only control in a DataTableToolbar. */
export interface DataTableToolbarButtonProps extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, 'children'> {
  /** Shown in the tooltip and used as the accessible name, since the control has no visible text. */
  label: string
  icon: ReactNode
  /** Renders a locale-aware link instead of a button. */
  href?: string
  /** Marks a toggle that is on, such as an open filter panel. */
  active?: boolean
  /** Overlaid on the icon, such as a count badge. */
  children?: ReactNode
}

/**
 * DataTableToolbarButton is the icon-only control every toolbar action uses (filter toggle, create link,
 * export trigger, ...), so they look alike and line up with the search input. The label lives in a tooltip,
 * and a disabled control keeps the not-allowed cursor and loses its hover. It forwards its ref to the
 * underlying button, so it can serve as a Headless UI `MenuButton` via `as`.
 */
export const DataTableToolbarButton = forwardRef<HTMLButtonElement, DataTableToolbarButtonProps>(
  ({ label, icon, href, active, className, children, type = 'button', ...props }, ref) => {
    const classes = cn(toolbarButtonVariants({ active }), className)

    return (
      <Tooltip content={label}>
        {href ? (
          <LocalizedLink href={href} className={classes} aria-label={label}>
            {icon}
            {children}
          </LocalizedLink>
        ) : (
          <button ref={ref} type={type} className={classes} aria-label={label} {...props}>
            {icon}
            {children}
          </button>
        )}
      </Tooltip>
    )
  },
)

DataTableToolbarButton.displayName = 'DataTableToolbarButton'
