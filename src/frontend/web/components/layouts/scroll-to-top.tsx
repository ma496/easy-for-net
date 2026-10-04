'use client'
import React, { useEffect, useState } from 'react'
import { ArrowUp } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useTranslation } from '@/i18n'

/**
 * Floating "back to top" button that fades in once the page has scrolled past 300px and smoothly scrolls the document back to the top on click.
 */
export const ScrollToTop = () => {
  const [visible, setVisible] = useState(false)
  const { t } = useTranslation()

  useEffect(() => {
    const onScroll = () => setVisible(window.scrollY > 300)
    window.addEventListener('scroll', onScroll, { passive: true })
    return () => window.removeEventListener('scroll', onScroll)
  }, [])

  return (
    <button
      type="button"
      aria-label={t('common.scrollToTop')}
      onClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })}
      className={cn(
        'fixed end-6 bottom-6 z-40 flex size-10 items-center justify-center rounded-full border border-border bg-surface text-muted-foreground shadow-md transition-all duration-200 hover:text-foreground',
        visible ? 'translate-y-0 opacity-100' : 'pointer-events-none translate-y-2 opacity-0',
      )}
    >
      <ArrowUp size={18} />
    </button>
  )
}
