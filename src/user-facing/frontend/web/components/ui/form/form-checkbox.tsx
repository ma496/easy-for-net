'use client'

import { useField, useFormikContext } from 'formik'
import { cn } from '@/lib/utils'
import { VariantProps, cva } from 'class-variance-authority'
import { useId } from 'react'

const formCheckboxVariants = cva('form-checkbox cursor-pointer rounded-sm', {
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

type InputAttributes = Omit<React.InputHTMLAttributes<HTMLInputElement>, 'size'>

/** Props for the Formik-aware FormCheckbox, which connects to a form field by name and shows validation errors after the field is dirty or the form has been submitted. */
interface FormCheckboxProps extends InputAttributes, VariantProps<typeof formCheckboxVariants> {
  label?: string
  name: string
  showValidation?: boolean
  className?: string
  required?: boolean
}

/**
 * FormCheckbox is a client component that uses Formik's useField and useFormikContext to bind a styled checkbox to a form field and display validation errors after the field is dirty or the form has been submitted.
 */
export const FormCheckbox = ({ label, name, id, showValidation = true, className, variant, size, required = false, ...props }: FormCheckboxProps) => {
  const [field, meta] = useField({ name, type: 'checkbox' })
  const { submitCount } = useFormikContext()
  const isDirty = meta.initialValue !== meta.value
  const hasError = (isDirty || submitCount > 0) && meta.error
  const defaultId = useId()
  const inputId = id ?? defaultId

  return (
    <div className={cn('inline-flex flex-col items-start', className, (isDirty || submitCount > 0) && (hasError ? 'has-error' : ''))}>
      <div className="flex min-h-5 items-center">
        <input {...field} {...props} type="checkbox" id={inputId} name={name} className={cn('mb-0', formCheckboxVariants({ variant, size }), hasError && 'border-danger focus:ring-danger')} />
        {label && (
          <label
            htmlFor={inputId}
            className={cn('ms-1 mb-0 flex cursor-pointer items-center leading-none select-none', size === 'sm' && 'text-xs', size === 'default' && 'text-sm', size === 'lg' && 'text-base')}
          >
            {label}
            {required && <span className="ms-1 text-danger">*</span>}
          </label>
        )}
      </div>
      {showValidation && (isDirty || submitCount > 0) && hasError && <div className="mt-1.5 text-xs font-medium text-danger">{meta.error}</div>}
    </div>
  )
}

export { formCheckboxVariants }
