'use client'

import { RefObject, useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react'

/** Viewport coordinates and size a dropdown panel is rendered at, in the fixed-position coordinate space. */
export interface DropdownPosition {
  /** Distance from the top of the viewport to the panel's top edge; set only when the panel opens below the anchor. */
  top?: number
  /**
   * Distance from the bottom of the viewport to the panel's bottom edge; set only when the panel is flipped above the
   * anchor. Pinning the bottom edge keeps a panel shorter than its maximum height against the anchor instead of
   * leaving it floating where a full-height panel would have started.
   */
  bottom?: number
  left: number
  width: number
  maxHeight: number
  /** True when the panel was flipped above the anchor because there was more room there. */
  flipped: boolean
  /**
   * Stacking order for the panel. On a page it sits beneath the sticky header, so a field scrolled up under the header
   * takes its panel under it too rather than drawing the panel over the header; inside a dialog it must sit above the
   * dialog's own layer instead.
   */
  zIndex: number
}

/** Distance kept between the anchor and the panel, and between the panel and the edge of the viewport. */
const GAP = 4
const VIEWPORT_MARGIN = 8
/** A panel shorter than this is not worth opening into the smaller side, so the larger side always wins below. */
const MIN_HEIGHT = 120
/** Below the sticky header (z-30) on a page; above every overlay (the modal is z-90) inside a dialog. */
const PAGE_Z_INDEX = 20
const DIALOG_Z_INDEX = 999

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
 * The side and the height are decided once, when the panel opens. Scrolling (captured, so scrolling of any ancestor
 * counts) and resizing only move the panel along with the anchor: re-deciding them there would make the panel jump from
 * one side of the field to the other, and grow or shrink, while the page moves under it. The position is deliberately
 * not recomputed on every render, so a caller that needs a fresh measurement after changing the anchor's size should
 * close and reopen the panel.
 */
export const useDropdownPosition = (anchorRef: RefObject<HTMLElement | null>, open: boolean, preferredMaxHeight: number): DropdownPosition | undefined => {
  const [position, setPosition] = useState<DropdownPosition>()
  // The side and height chosen when the panel opened; cleared on close so the next opening decides afresh.
  const placementRef = useRef<{ flipped: boolean; maxHeight: number; zIndex: number } | undefined>(undefined)

  const decide = useCallback((anchor: HTMLElement, rect: DOMRect) => {
    const spaceBelow = window.innerHeight - rect.bottom - GAP - VIEWPORT_MARGIN
    const spaceAbove = rect.top - GAP - VIEWPORT_MARGIN
    // Stay below unless the panel would be cramped there and there is genuinely more room above.
    const flipped = spaceBelow < Math.min(preferredMaxHeight, MIN_HEIGHT) && spaceAbove > spaceBelow
    const available = Math.max(flipped ? spaceAbove : spaceBelow, MIN_HEIGHT)
    const maxHeight = Math.min(preferredMaxHeight, available)
    const zIndex = anchor.closest('[role="dialog"]') ? DIALOG_Z_INDEX : PAGE_Z_INDEX
    return { flipped, maxHeight, zIndex }
  }, [preferredMaxHeight])

  const compute = useCallback(() => {
    const anchor = anchorRef.current
    if (!anchor) return

    const rect = anchor.getBoundingClientRect()
    placementRef.current ??= decide(anchor, rect)
    const { flipped, maxHeight, zIndex } = placementRef.current

    setPosition({
      top: flipped ? undefined : rect.bottom + GAP,
      bottom: flipped ? window.innerHeight - rect.top + GAP : undefined,
      left: rect.left,
      width: rect.width,
      maxHeight,
      flipped,
      zIndex,
    })
  }, [anchorRef, decide])

  // Measured before paint so the panel never shows up at a stale position for a frame.
  useIsomorphicLayoutEffect(() => {
    if (!open) {
      placementRef.current = undefined
      return
    }
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
