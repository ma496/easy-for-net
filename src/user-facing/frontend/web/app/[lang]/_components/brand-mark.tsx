'use client'

import { useTranslation } from '@/i18n'
import { LocalizedLink } from '@/components/ui'
import { LogoMark } from '@/components/custom'
import { cn } from '@/lib/utils'

/**
 * Props for the {@link BrandMark} component.
 */
interface BrandMarkProps {
  /** Where the mark links to; the landing page unless a screen names somewhere else. */
  href?: string
  /** Hides the brand name below the `sm` breakpoint, leaving only the logo tile, where a top bar is crowded. */
  compactOnPhone?: boolean
  className?: string
}

/**
 * The logo tile and brand name used across the public, auth and status screens, matching the admin sidebar's mark.
 */
export const BrandMark = ({ href = '/', compactOnPhone = false, className }: BrandMarkProps) => {
  const { t } = useTranslation()

  return (
    <LocalizedLink href={href} className={cn('flex min-w-0 items-center gap-2.5 rounded-md focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none', className)}>
      <LogoMark />
      <span className={cn('truncate text-[15px] font-semibold tracking-tight text-foreground', compactOnPhone && 'max-sm:sr-only')}>{t('brand.name') || 'Easy For Net'}</span>
    </LocalizedLink>
  )
}
