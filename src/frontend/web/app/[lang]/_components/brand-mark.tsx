'use client'

import Image from 'next/image'
import { useTranslation } from '@/i18n'
import { LocalizedLink } from '@/components/ui'
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
      <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-primary/10 ring-1 ring-primary/20">
        <Image className="size-5" src="/assets/images/icon.png" alt="logo" width={20} height={20} unoptimized priority />
      </span>
      <span className={cn('truncate text-[15px] font-semibold tracking-tight text-foreground', compactOnPhone && 'max-sm:sr-only')}>{t('brand.name')}</span>
    </LocalizedLink>
  )
}
