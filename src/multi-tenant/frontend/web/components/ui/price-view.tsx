import { cn } from '@/lib/utils'

/** Props for the PriceView component, which formats a numeric or numeric-string amount as a localized currency string and handles empty/invalid inputs gracefully. */
export interface PriceViewProps {
  amount?: number | string
  currency?: string
  locale?: string
  className?: string
  placeholder?: string
  decimals?: number
}

/**
 * PriceView renders a localized currency-formatted amount using Intl.NumberFormat, displaying a muted placeholder when the value is missing and an "Invalid Price" label for non-numeric input.
 */
export const PriceView = ({
  amount,
  currency = 'USD',
  locale = 'en-US',
  className,
  placeholder = '-',
  decimals = 2,
}: PriceViewProps) => {
  if (amount === undefined || amount === '') {
    return <span className={cn('text-subtle-foreground', className)}>{placeholder}</span>
  }

  const numericAmount = typeof amount === 'string' ? parseFloat(amount) : amount

  if (isNaN(numericAmount)) {
    return <span className={cn('text-danger', className)}>Invalid Price</span>
  }

  const formattedPrice = new Intl.NumberFormat(locale, {
    style: 'currency',
    currency: currency,
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  }).format(numericAmount)

  return <span className={cn(className)}>{formattedPrice}</span>
}
