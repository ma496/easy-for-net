'use client'

import { ReactNode } from 'react'
import { Menu, MenuButton, MenuItem, MenuItems } from '@headlessui/react'
import { MoreHorizontal } from 'lucide-react'
import { cva } from 'class-variance-authority'
import { cn } from '@/lib/utils'
import { useTranslation } from '@/i18n'
import { useAppSelector } from '@/store/hooks'
import { LocalizedLink } from '../localized-link'

const rowActionItemVariants = cva(
  'flex w-full cursor-pointer items-center gap-2 px-4 py-2 text-sm whitespace-nowrap data-disabled:cursor-not-allowed data-disabled:opacity-50 ltr:text-left rtl:text-right',
  {
    variants: {
      variant: {
        default: 'data-focus:bg-primary/10 data-focus:text-primary',
        primary: 'text-primary data-focus:bg-primary/10',
        success: 'text-success data-focus:bg-success/10',
        warning: 'text-warning data-focus:bg-warning/10',
        danger: 'text-danger data-focus:bg-danger/10',
      },
    },
    defaultVariants: {
      variant: 'default',
    },
  },
)

/** One entry in a row's action menu: a locale-aware link when `href` is set, a button running `onClick` otherwise. */
export interface DataTableRowAction {
  label: string
  icon?: ReactNode
  href?: string
  onClick?: () => void
  variant?: 'default' | 'primary' | 'success' | 'warning' | 'danger'
  disabled?: boolean
  /** Leaves the entry out, so callers can list every action and gate each one inline. */
  hidden?: boolean
}

/** Props for DataTableRowActions, the per-row action menu of a data table. */
interface DataTableRowActionsProps {
  actions: DataTableRowAction[]
  className?: string
}

/**
 * DataTableRowActions renders a row's actions behind a three-dot trigger. The menu is portaled and
 * anchored to the trigger, so the table's scroll container never clips it, and it renders nothing
 * when every action is hidden.
 */
export function DataTableRowActions({ actions, className }: DataTableRowActionsProps) {
  const { t } = useTranslation()
  const isRTL = useAppSelector((state) => state.theme.rtlClass) === 'rtl'
  const visible = actions.filter((action) => !action.hidden)

  if (visible.length === 0) return null

  return (
    <Menu>
      <MenuButton
        className={cn('btn btn-outline-secondary btn-sm cursor-pointer p-1.5! shadow-none', className)}
        aria-label={t('table.actions')}
        title={t('table.actions')}
      >
        <MoreHorizontal className="h-4 w-4" />
      </MenuButton>
      <MenuItems
        anchor={isRTL ? 'bottom start' : 'bottom end'}
        modal={false}
        className="z-50 mt-1 min-w-40 rounded-md bg-white py-2 text-black shadow-lg ring-1 ring-black/5 focus:outline-none dark:bg-[#1b2e4b] dark:text-white-dark"
      >
        {visible.map((action) => {
          const content = (
            <>
              {action.icon}
              <span>{action.label}</span>
            </>
          )
          const itemClassName = rowActionItemVariants({ variant: action.variant })

          return (
            <MenuItem key={action.label} disabled={action.disabled}>
              {action.href ? (
                <LocalizedLink href={action.href} className={itemClassName}>
                  {content}
                </LocalizedLink>
              ) : (
                <button type="button" className={itemClassName} onClick={action.onClick}>
                  {content}
                </button>
              )}
            </MenuItem>
          )
        })}
      </MenuItems>
    </Menu>
  )
}
