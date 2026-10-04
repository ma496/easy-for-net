'use client'

import { cn } from '@/lib/utils'
import { VariantProps, cva } from 'class-variance-authority'
import { useId } from 'react'

const checkboxVariants = cva('form-checkbox cursor-pointer rounded-sm', {
  variants: {
    variant: {
      default: 'text-primary border-input focus:ring-primary',
      success: 'text-success border-success focus:ring-success',
      danger: 'text-danger border-danger focus:ring-danger',
      warning: 'text-warning border-warning focus:ring-warning',
      info: 'text-info border-info focus:ring-info',
      secondary: 'text-secondary border-secondary focus:ring-secondary',
      dark: 'text-foreground border-input focus:ring-ring',
    },
    size: {
      default: 'h-4 w-4',
      sm: 'h-3 w-3',
      lg: 'h-5 w-5',
    },
  },
  defaultVariants: {
    variant: 'default',
    size: 'default',
  },
})

/** Props for the standalone Checkbox component, a native checkbox with an optional label, color/size variants, and external error display. */
interface CheckboxProps extends Omit<React.InputHTMLAttributes<HTMLInputElement>, 'size'>, VariantProps<typeof checkboxVariants> {
  label?: string
  name: string
  className?: string
  error?: string
  showError?: boolean
  required?: boolean
}

/**
 * Checkbox is a client component that renders a styled native checkbox with an optional label and an externally provided error message (used outside Formik contexts).
 */
export const Checkbox = ({ label, name, className, variant, size, error, showError = true, id, required = false, ...props }: CheckboxProps) => {
  const defaultId = useId()
  const checkboxId = id ?? defaultId

  return (
    <div className={cn('inline-flex flex-col items-start', className, error && showError && 'has-error')}>
      <div className="flex min-h-5 items-center">
        <input {...props} type="checkbox" id={checkboxId} name={name} className={cn('mb-0', checkboxVariants({ variant, size }), error && 'border-danger focus:ring-danger')} />
        {label && (
          <label
            htmlFor={checkboxId}
            className={cn('ms-1 mb-0 flex cursor-pointer items-center leading-none select-none', size === 'sm' && 'text-xs', size === 'default' && 'text-sm', size === 'lg' && 'text-base')}
          >
            {label}
            {required && <span className="ms-1 text-danger">*</span>}
          </label>
        )}
      </div>
      {showError && error && <div className="mt-1.5 text-xs font-medium text-danger">{error}</div>}
    </div>
  )
}
