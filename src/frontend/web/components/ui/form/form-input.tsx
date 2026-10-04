'use client'

import { useField, useFormikContext } from 'formik'
import { cn } from '@/lib/utils'
import { useId } from 'react'

/** Props for the Formik-aware FormInput, a text-style input bound to a form field by name with optional label, leading icon, and validation display. */
interface FormInputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string
  name: string
  showValidation?: boolean
  className?: string
  icon?: React.ReactNode
  required?: boolean
}

/**
 * FormInput is a client component that uses Formik's useField and useFormikContext to bind a styled text input to a form field, supporting an optional leading icon and showing validation errors after the field is dirty or the form has been submitted.
 */
export const FormInput = ({ label, name, id, showValidation = true, className, icon, autoComplete = 'off', required = false, ...props }: FormInputProps) => {
  const [field, meta] = useField(name)
  const { submitCount } = useFormikContext()
  const isDirty = meta.initialValue !== meta.value
  const hasError = (isDirty || submitCount > 0) && meta.error
  const generatedId = useId()
  const inputId = id ?? generatedId

  return (
    <div className={cn(className, (isDirty || submitCount > 0) && (hasError ? 'has-error' : ''))}>
      {label && (
        <label htmlFor={inputId} className="label form-label">
          {label}
          {required && <span className="ms-1 text-danger">*</span>}
        </label>
      )}
      <div className="relative text-muted-foreground">
        <input {...field} {...props} id={inputId} name={name} autoComplete={autoComplete} className={cn('form-input', icon && 'ps-10')} />
        {icon && <span className="pointer-events-none absolute inset-s-3 top-1/2 -translate-y-1/2">{icon}</span>}
      </div>
      {showValidation && (isDirty || submitCount > 0) && hasError && <div className="mt-1.5 text-xs font-medium text-danger">{meta.error}</div>}
    </div>
  )
}
