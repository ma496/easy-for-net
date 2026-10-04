'use client'
import { usePathname } from 'next/navigation'
import React, { useEffect, useRef } from 'react'

/**
 * ContentAnimation wraps the page content in the shell's centred column and replays a short fade-up on every route change.
 * The animation is restarted on the element rather than by remounting it, so the page's state survives a navigation.
 */
export const ContentAnimation = ({ children }: { children: React.ReactNode }) => {
  const pathname = usePathname()
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const element = ref.current
    if (!element) return
    element.classList.remove('animate-fade-up')
    // Reading layout flushes the removal, so re-adding the class starts the animation again.
    void element.offsetWidth
    element.classList.add('animate-fade-up')
  }, [pathname])

  return (
    <div ref={ref} className="mx-auto w-full max-w-[1400px] animate-fade-up px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
      {children}
    </div>
  )
}
