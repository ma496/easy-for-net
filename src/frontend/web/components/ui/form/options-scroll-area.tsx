'use client'

import { useEffect, useRef, type ReactNode } from 'react'
import ScrollBar from 'react-perfect-scrollbar'

/** Props for OptionsScrollArea, the scrolling option list inside a select's dropdown panel. */
interface OptionsScrollAreaProps {
  /** Height in pixels past which the list scrolls. */
  maxHeight: number
  isRTL: boolean
  /** Called on vertical scroll, e.g. to load the next page of a lazy list. */
  onScrollY?: (container: HTMLElement) => void
  children: ReactNode
}

/**
 * OptionsScrollArea is the vertical scroll area every select's option list renders in. Its rail appears only when the
 * options actually overflow `maxHeight`.
 */
export function OptionsScrollArea({ maxHeight, isRTL, onScrollY, children }: OptionsScrollAreaProps) {
  const scrollBarRef = useRef<ScrollBar>(null)
  const containerRef = useRef<HTMLElement | null>(null)

  // perfect-scrollbar measures only when it mounts or re-renders, and a dropdown is first laid out before its panel
  // has been positioned and sized, so the list can be measured taller than it ends up and keep a rail it does not
  // need. Re-measure whenever the list or its options change size.
  useEffect(() => {
    const container = containerRef.current
    if (!container || typeof ResizeObserver === 'undefined') return
    const observer = new ResizeObserver(() => scrollBarRef.current?.updateScroll())
    observer.observe(container)
    if (container.firstElementChild) observer.observe(container.firstElementChild)
    return () => observer.disconnect()
  }, [isRTL])

  return (
    <ScrollBar
      ref={scrollBarRef}
      containerRef={(element) => {
        containerRef.current = element
      }}
      // A fraction of a pixel of rounding between the list and its options is not overflow.
      options={{ suppressScrollX: true, scrollYMarginOffset: 1 }}
      style={{ maxHeight: `${maxHeight}px`, direction: isRTL ? 'rtl' : 'ltr' }}
      onScrollY={onScrollY}
      // perfect-scrollbar places its rail for the direction it mounted in, so a direction change remounts it.
      key={isRTL ? 'rtl' : 'ltr'}
    >
      {children}
    </ScrollBar>
  )
}
