import React, { ButtonHTMLAttributes } from 'react'
import { VariantProps, cva } from 'class-variance-authority'
import { cn } from '@/lib/utils'
import { Loader2 } from 'lucide-react'

const buttonVariants = cva('btn', {
  variants: {
    variant: {
      default: 'btn-primary',
      primary: 'btn-primary',
      // A neutral bordered button: the usual pairing beside a primary action (cancel, back, secondary actions).
      outline: 'btn-secondary',
      secondary: 'btn-secondary',
      soft: 'btn-soft',
      ghost: 'btn-ghost',
      link: 'h-auto px-0 text-primary underline-offset-4 hover:underline',
      info: 'btn-info',
      'outline-info': 'btn-outline-info',
      success: 'btn-success',
      'outline-success': 'btn-outline-success',
      warning: 'btn-warning',
      'outline-warning': 'btn-outline-warning',
      danger: 'btn-danger',
      'outline-danger': 'btn-outline-danger',
      'outline-primary': 'btn-outline-primary',
      'outline-secondary': 'btn-secondary',
      dark: 'btn-dark',
      'outline-dark': 'btn-outline-dark',
    },
    size: {
      default: "[&_svg:not([class*='size-'])]:size-4",
      sm: "btn-sm [&_svg:not([class*='size-'])]:size-3.5",
      lg: "btn-lg [&_svg:not([class*='size-'])]:size-5",
    },
    rounded: {
      default: '',
      full: 'rounded-full',
    },
  },
  defaultVariants: {
    variant: 'default',
    size: 'default',
    rounded: 'default',
  },
})

/**
 * Props for the Button component, a styled native button element that supports variants, sizes, loading state, and an optional leading icon.
 */
export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement>, VariantProps<typeof buttonVariants> {
  isLoading?: boolean
  icon?: React.ReactNode
}

/**
 * Button is a styled native button with variant/size/rounded options that supports a loading spinner state and an optional leading icon.
 * It forwards a ref to the underlying HTMLButtonElement and is automatically disabled while loading.
 */
const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(({ className, variant, size, rounded, isLoading, disabled, children, icon, ...props }, ref) => {
  return (
    <button className={cn(buttonVariants({ variant, size, rounded }), className)} ref={ref} disabled={disabled || isLoading} {...props}>
      {isLoading && <Loader2 className="animate-spin" />}
      {icon && !isLoading && <span className="inline-flex shrink-0">{icon}</span>}
      {children}
    </button>
  )
})

Button.displayName = 'Button'

export { Button, buttonVariants }
