'use client'

import { useField, useFormikContext } from 'formik'
import { cn } from '@/lib/utils'
import { useId } from 'react'

/** Props for the Formik-aware FormTextarea, a multi-line text input bound to a form field by name with an optional label. */
interface FormTextareaProps extends React.TextareaHTMLAttributes<HTMLTextAreaElement> {
  label?: string
  name: string
  showValidation?: boolean
  className?: string
  required?: boolean
}

/**
 * FormTextarea is a client component that uses Formik's useField/useFormikContext to bind a styled textarea to a form field, showing validation errors after the field is dirty or the form has been submitted.
 */
export const FormTextarea = ({ label, name, id, showValidation = true, className, autoComplete = 'off', required = false, ...props }: FormTextareaProps) => {
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
        <textarea {...field} {...props} name={name} id={inputId} autoComplete={autoComplete} className="form-textarea" />
      </div>
      {showValidation && (isDirty || submitCount > 0) && hasError && <div className="mt-1.5 text-xs font-medium text-danger">{meta.error}</div>}
    </div>
  )
}
