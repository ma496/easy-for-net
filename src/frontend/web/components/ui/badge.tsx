'use client'

import React from 'react'
import { cva, type VariantProps } from 'class-variance-authority'
import { cn } from '@/lib/utils'

const colors = ['primary', 'secondary', 'success', 'danger', 'warning', 'info', 'dark'] as const

const badgeVariants = cva('badge', {
  variants: {
    variant: Object.fromEntries(colors.map((color) => [color, ''])) as Record<(typeof colors)[number], string>,
    // `solid` (the default) is a soft tint of the color; `strong` fills it; `outline` draws only the border.
    type: {
      solid: '',
      strong: '',
      outline: '',
    },
  },
  compoundVariants: colors.flatMap((color) => [
    { variant: color, type: 'solid' as const, className: `badge-${color}` },
    { variant: color, type: 'strong' as const, className: `badge-solid-${color}` },
    { variant: color, type: 'outline' as const, className: `badge-outline-${color}` },
  ]),
  defaultVariants: {
    variant: 'primary',
    type: 'solid',
  },
})

/**
 * Props for the Badge component, a small label-like element rendered as a span with variant and style options.
 */
export interface IBadgeProps extends React.HTMLAttributes<HTMLSpanElement>, VariantProps<typeof badgeVariants> {
  children: React.ReactNode
}

/**
 * Badge is a small pill label that supports color variants (primary, secondary, success, danger, warning, info, dark) and soft (`solid`), filled (`strong`) or outline styles.
 */
export const Badge: React.FC<IBadgeProps> = ({ children, variant, type, className, ...props }) => {
  return (
    <span className={cn(badgeVariants({ variant, type }), className)} {...props}>
      {children}
    </span>
  )
}

export { badgeVariants }
