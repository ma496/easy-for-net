'use client'

import { useId, useState } from 'react'
import { useField, useFormikContext } from 'formik'
import { cn } from '@/lib/utils'
import { Eye, EyeOff } from 'lucide-react'

/** Props for the Formik-aware FormPasswordInput, a password input with a built-in show/hide toggle bound to a form field by name. */
interface FormPasswordInputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string
  name: string
  showValidation?: boolean
  className?: string
  icon?: React.ReactNode
  required?: boolean
}

/**
 * FormPasswordInput is a client component that uses Formik's useField and useFormikContext to bind a password-style input to a form field, with an inline eye-icon toggle for showing/hiding the value and validation errors after the field is dirty or the form has been submitted.
 */
export const FormPasswordInput = ({ label, name, id, showValidation = true, className, icon, autoComplete = 'off', required = false, ...props }: FormPasswordInputProps) => {
  const [field, meta] = useField(name)
  const { submitCount } = useFormikContext()
  const isDirty = meta.initialValue !== meta.value
  const [showPassword, setShowPassword] = useState(false)
  const hasError = (isDirty || submitCount > 0) && meta.error
  const generatedId = useId()
  const inputId = id ?? generatedId

  const togglePasswordVisibility = () => {
    setShowPassword(!showPassword)
  }

  return (
    <div className={cn(className, (isDirty || submitCount > 0) && (hasError ? 'has-error' : ''))}>
      {label && (
        <label htmlFor={inputId} className="form-label">
          {label}
          {required && <span className="ms-1 text-danger">*</span>}
        </label>
      )}
      <div className="relative text-muted-foreground">
        <input {...field} {...props} name={name} id={inputId} autoComplete={autoComplete} type={showPassword ? 'text' : 'password'} className={cn('form-input', icon && 'ps-10', 'pe-10')} />
        {icon && <span className="pointer-events-none absolute inset-s-3 top-1/2 -translate-y-1/2">{icon}</span>}
        <button type="button" onClick={togglePasswordVisibility} tabIndex={-1} className="absolute inset-e-3 top-1/2 -translate-y-1/2 text-subtle-foreground hover:text-muted-foreground focus:outline-hidden">
          {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
        </button>
      </div>
      {showValidation && (isDirty || submitCount > 0) && hasError && <div className="mt-1.5 text-xs font-medium text-danger">{meta.error}</div>}
    </div>
  )
}
