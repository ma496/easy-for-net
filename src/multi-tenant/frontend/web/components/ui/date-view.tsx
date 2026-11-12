import { format as formatDate, isValid } from 'date-fns'
import { cn } from '@/lib/utils'

/** Props for the DateView component, which formats a date value for display and handles empty/invalid inputs gracefully. */
export interface DateViewProps {
  date?: Date | string | number | null
  format?: string
  className?: string
  placeholder?: string
}

/**
 * DateView renders a date formatted with date-fns, showing a muted placeholder for empty values and an "Invalid Date" label for unparseable inputs.
 */
export const DateView = ({
  date,
  format = 'dd MMM yyyy',
  className,
  placeholder = '-'
}: DateViewProps) => {
  if (!date) {
    return <span className={cn('text-subtle-foreground', className)}>{placeholder}</span>
  }

  const dateObj = new Date(date)

  if (!isValid(dateObj)) {
    return <span className={cn('text-danger', className)}>Invalid Date</span>
  }

  return (
    <span className={cn(className)}>
      {formatDate(dateObj, format)}
    </span>
  )
}
