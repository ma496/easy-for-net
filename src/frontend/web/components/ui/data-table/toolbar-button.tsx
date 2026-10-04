'use client'

import { ButtonHTMLAttributes, forwardRef, ReactNode } from 'react'
import { cva } from 'class-variance-authority'
import { cn } from '@/lib/utils'
import { Tooltip } from '../tooltip'
import { LocalizedLink } from '../localized-link'

const toolbarButtonVariants = cva(
  'relative inline-flex size-9 shrink-0 cursor-pointer items-center justify-center rounded-md border shadow-xs transition-colors duration-150 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-hidden disabled:cursor-not-allowed disabled:opacity-50',
  {
    variants: {
      active: {
        false:
          'border-border bg-surface text-muted-foreground not-disabled:hover:bg-surface-2 not-disabled:hover:text-foreground',
        true: 'border-primary/40 bg-primary/10 text-primary',
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
  /** The screen's main action (such as create): a solid accent icon button. */
  primary?: boolean
}

/**
 * DataTableToolbarButton is the icon-only control every toolbar action uses (filter toggle, create link,
 * export trigger, ...), so they look alike and line up with the search input. The label lives in a tooltip,
 * and a disabled control keeps the not-allowed cursor and loses its hover. It forwards its ref to the
 * underlying button, so it can serve as a Headless UI `MenuButton` via `as`. `primary` turns it into the
 * screen's main action - the same icon-only square, filled with the accent colour.
 */
export const DataTableToolbarButton = forwardRef<HTMLButtonElement, DataTableToolbarButtonProps>(
  ({ label, icon, href, active, primary, className, children, type = 'button', ...props }, ref) => {
    const classes = cn(primary ? 'btn btn-primary size-9 p-0' : toolbarButtonVariants({ active }), className)
    const content = (
      <>
        {icon}
        {children}
      </>
    )

    const control = href ? (
      <LocalizedLink href={href} className={classes} aria-label={label}>
        {content}
      </LocalizedLink>
    ) : (
      <button ref={ref} type={type} className={classes} aria-label={label} {...props}>
        {content}
      </button>
    )

    return <Tooltip content={label}>{control}</Tooltip>
  },
)

DataTableToolbarButton.displayName = 'DataTableToolbarButton'
