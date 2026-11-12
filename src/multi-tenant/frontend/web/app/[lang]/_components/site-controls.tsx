'use client'

import { LanguageDropdown, ThemeChanger } from '@/components/custom'
import { useAppSelector } from '@/store/hooks'
import { cn } from '@/lib/utils'

/**
 * The language and theme switchers shown in the top corner of the public, auth and status screens,
 * so a visitor who is not signed in can change both without reaching the admin header.
 */
export const SiteControls = ({ className }: { className?: string }) => {
  const theme = useAppSelector((state) => state.theme.theme)

  return (
    <div className={cn('flex shrink-0 items-center gap-1.5', className)}>
      <LanguageDropdown />
      <ThemeChanger theme={theme} />
    </div>
  )
}
