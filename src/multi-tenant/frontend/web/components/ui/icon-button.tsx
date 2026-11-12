import React, { ButtonHTMLAttributes } from 'react'
import { VariantProps, cva } from 'class-variance-authority'
import { cn } from '@/lib/utils'
import { Loader2 } from 'lucide-react'

const iconButtonVariants = cva("btn aspect-square p-0! [&_svg:not([class*='size-'])]:size-4", {
  variants: {
    variant: {
      default: 'btn-primary',
      primary: 'btn-primary',
      outline: 'btn-secondary',
      secondary: 'btn-secondary',
      soft: 'btn-soft',
      ghost: 'btn-ghost',
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
      sm: 'size-8',
      default: 'size-9',
      lg: "size-11 [&_svg:not([class*='size-'])]:size-5",
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

/** Props for the IconButton component, a square aspect-ratio button that displays a single icon and supports a loading state. */
interface IconButtonProps extends ButtonHTMLAttributes<HTMLButtonElement>, VariantProps<typeof iconButtonVariants> {
  icon: React.ReactNode
  isLoading?: boolean
}

/**
 * IconButton is a square, icon-only button built on the same variant system as Button that swaps its icon for a spinner while loading.
 * It forwards a ref to the underlying HTMLButtonElement.
 */
const IconButton = React.forwardRef<HTMLButtonElement, IconButtonProps>(({ className, variant, size, rounded, icon, isLoading, disabled, ...props }, ref) => {
  return (
    <button type="button" className={cn(iconButtonVariants({ variant, size, rounded }), className)} ref={ref} disabled={disabled || isLoading} {...props}>
      {isLoading ? <Loader2 className="animate-spin" /> : icon}
    </button>
  )
})

IconButton.displayName = 'IconButton'

export { IconButton, iconButtonVariants }
