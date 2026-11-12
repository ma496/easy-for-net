'use client'

import { cn } from '@/lib/utils'
import { useId } from 'react'

/** Props for the standalone Input component, a text-style input with an optional label, leading icon, and externally provided error display (used outside Formik contexts). */
interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string
  name: string
  className?: string
  icon?: React.ReactNode
  error?: string
  showError?: boolean
  required?: boolean
}

/**
 * Input is a client component that renders a styled native input with an optional leading icon and externally provided error message.
 */
export const Input = ({ label, name, id, className, icon, error, showError = true, autoComplete = 'off', required = false, ...props }: InputProps) => {
  const generatedId = useId()
  const inputId = id ?? generatedId

  return (
    <div className={cn('w-full', className, error && showError && 'has-error')}>
      {label && (
        <label htmlFor={inputId} className="label form-label">
          {label}
          {required && <span className="ms-1 text-danger">*</span>}
        </label>
      )}
      <div className="relative text-muted-foreground">
        <input {...props} name={name} id={inputId} autoComplete={autoComplete} className={cn('form-input', icon && 'ps-10')} />
        {icon && <span className="pointer-events-none absolute inset-s-3 top-1/2 -translate-y-1/2">{icon}</span>}
      </div>
      {showError && error && <div className="mt-1.5 text-xs font-medium text-danger">{error}</div>}
    </div>
  )
}
