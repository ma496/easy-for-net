'use client'

import { cn } from '@/lib/utils'
import { useId } from 'react'

/** Props for the standalone Textarea, a multi-line input with an optional label and externally provided error display. */
interface TextareaProps extends React.TextareaHTMLAttributes<HTMLTextAreaElement> {
  label?: string
  name: string
  className?: string
  error?: string
  showError?: boolean
  required?: boolean
}

/**
 * Textarea is a client component that renders a styled native textarea with an optional label and an externally provided error message.
 */
export const Textarea = ({ label, name, id, className, error, showError = true, autoComplete = 'off', required = false, ...props }: TextareaProps) => {
  const generatedId = useId()
  const textareaId = id ?? generatedId

  return (
    <div className={cn(className, error && showError && 'has-error')}>
      {label && (
        <label htmlFor={textareaId} className="label form-label">
          {label}
          {required && <span className="ms-1 text-danger">*</span>}
        </label>
      )}
      <div className="relative text-muted-foreground">
        <textarea {...props} name={name} id={textareaId} autoComplete={autoComplete} className="form-textarea" />
      </div>
      {showError && error && <div className="mt-1.5 text-xs font-medium text-danger">{error}</div>}
    </div>
  )
}
