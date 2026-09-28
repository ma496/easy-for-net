'use client'

import { RefObject, useCallback, useEffect, useLayoutEffect, useState } from 'react'

/** Viewport coordinates and size a dropdown panel is rendered at, in the fixed-position coordinate space. */
export interface DropdownPosition {
  top: number
  left: number
  width: number
  maxHeight: number
  /** True when the panel was flipped above the anchor because there was more room there. */
  flipped: boolean
}

/** Distance kept between the anchor and the panel, and between the panel and the edge of the viewport. */
const GAP = 4
const VIEWPORT_MARGIN = 8
/** A panel shorter than this is not worth opening into the smaller side, so the larger side always wins below. */
const MIN_HEIGHT = 120

/** React warns about useLayoutEffect during server rendering, where there is nothing to measure anyway. */
const useIsomorphicLayoutEffect = typeof window !== 'undefined' ? useLayoutEffect : useEffect

/**
 * Computes where a dropdown panel anchored to `anchorRef` should be drawn while it is open.
 *
 * The panel is meant to be rendered in a portal with `position: fixed` and these coordinates, which is what keeps it
 * visible when the field sits inside a container that clips its overflow - a modal panel, a card, a scrolling table.
 * Positioning it relative to the field instead would leave the part of the panel that reaches past the container
 * invisible. The panel is placed below the anchor when the remaining viewport height can hold it and flipped above it
 * otherwise, and its height is capped at whatever the chosen side actually offers so it always fits on screen.
 *
 * The position is recomputed on scroll (captured, so scrolling of any ancestor counts) and on resize; it is deliberately
 * not recomputed on every render, so a caller that needs a fresh measurement after changing the anchor's size should
 * close and reopen the panel.
 */
export const useDropdownPosition = (anchorRef: RefObject<HTMLElement | null>, open: boolean, preferredMaxHeight: number): DropdownPosition | undefined => {
  const [position, setPosition] = useState<DropdownPosition>()

  const compute = useCallback(() => {
    const anchor = anchorRef.current
    if (!anchor) return

    const rect = anchor.getBoundingClientRect()
    const spaceBelow = window.innerHeight - rect.bottom - GAP - VIEWPORT_MARGIN
    const spaceAbove = rect.top - GAP - VIEWPORT_MARGIN
    // Stay below unless the panel would be cramped there and there is genuinely more room above.
    const flipped = spaceBelow < Math.min(preferredMaxHeight, MIN_HEIGHT) && spaceAbove > spaceBelow
    const available = Math.max(flipped ? spaceAbove : spaceBelow, MIN_HEIGHT)
    const maxHeight = Math.min(preferredMaxHeight, available)

    setPosition({
      top: flipped ? rect.top - GAP - maxHeight : rect.bottom + GAP,
      left: rect.left,
      width: rect.width,
      maxHeight,
      flipped,
    })
  }, [anchorRef, preferredMaxHeight])

  // Measured before paint so the panel never shows up at a stale position for a frame.
  useIsomorphicLayoutEffect(() => {
    if (!open) return
    compute()
  }, [open, compute])

  useEffect(() => {
    if (!open) return

    const handle = () => compute()
    window.addEventListener('scroll', handle, true)
    window.addEventListener('resize', handle)
    return () => {
      window.removeEventListener('scroll', handle, true)
      window.removeEventListener('resize', handle)
    }
  }, [open, compute])

  return position
}
