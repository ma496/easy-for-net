'use client'

import { useId, useState } from 'react'
import { cn } from '@/lib/utils'
import { Eye, EyeOff } from 'lucide-react'

/** Props for the standalone PasswordInput, a password-style input with a built-in show/hide toggle and externally provided error display. */
interface PasswordInputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string
  name: string
  className?: string
  icon?: React.ReactNode
  error?: string
  showError?: boolean
  required?: boolean
}

/**
 * PasswordInput is a client component that renders a styled password input with an inline eye-icon toggle for revealing the value and an externally provided error message.
 */
export const PasswordInput = ({ label, name, id, className, icon, error, showError = true, autoComplete = 'off', required = false, ...props }: PasswordInputProps) => {
  const [showPassword, setShowPassword] = useState(false)
  const generatedId = useId()
  const inputId = id ?? generatedId

  const togglePasswordVisibility = () => {
    setShowPassword(!showPassword)
  }

  return (
    <div className={cn(className, error && showError && 'has-error')}>
      {label && (
        <label htmlFor={inputId} className="label form-label">
          {label}
          {required && <span className="ms-1 text-danger">*</span>}
        </label>
      )}
      <div className="relative text-muted-foreground">
        <input {...props} name={name} id={inputId} autoComplete={autoComplete} type={showPassword ? 'text' : 'password'} className={cn('form-input', icon && 'ps-10', 'pe-10')} />
        {icon && <span className="pointer-events-none absolute inset-s-3 top-1/2 -translate-y-1/2">{icon}</span>}
        <button type="button" onClick={togglePasswordVisibility} tabIndex={-1} className="absolute inset-e-3 top-1/2 -translate-y-1/2 text-subtle-foreground hover:text-muted-foreground focus:outline-hidden">
          {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
        </button>
      </div>
      {showError && error && <div className="mt-1.5 text-xs font-medium text-danger">{error}</div>}
    </div>
  )
}
