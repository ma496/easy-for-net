'use client'

import { Fragment, ReactNode, useCallback, useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { Transition } from '@headlessui/react'

/**
 * Props for the {@link RailFlyout} component.
 */
interface RailFlyoutProps {
  /** Only the desktop icon rail shows flyouts; when false the trigger renders on its own. */
  enabled: boolean
  /** A flyout the pointer can move into (a submenu) stays open across the gap and closes on a short delay; a label does not. */
  interactive?: boolean
  /** Clicking the trigger opens the flyout at once, for keyboard and touch users of a submenu. */
  openOnClick?: boolean
  /** The flyout's content; `close` dismisses it, e.g. once a link inside has been followed. */
  content: (close: () => void) => ReactNode
  /** The trigger, told whether its flyout is open (for `aria-expanded`). */
  children: (open: boolean) => ReactNode
}

/** The rail is the desktop layout only; below lg the sidebar is a full-width drawer. */
const desktopQuery = '(min-width: 1024px)'
const openDelay = 80
const closeDelay = 150
const gap = 8
const viewportMargin = 8

/**
 * A flyout beside an entry of the collapsed sidebar rail, opened by hovering or focusing the entry.
 * It is portaled to the body because the rail's scroll container clips what overflows it, positioned
 * against the rail's end edge (the left of an RTL rail), and kept inside the viewport vertically.
 */
export const RailFlyout = ({ enabled, interactive = false, openOnClick = false, content, children }: RailFlyoutProps) => {
  const [open, setOpen] = useState(false)
  const [wasEnabled, setWasEnabled] = useState(enabled)
  const triggerRef = useRef<HTMLDivElement>(null)
  const flyoutRef = useRef<HTMLDivElement>(null)
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  const clearTimer = () => {
    if (timerRef.current) {
      clearTimeout(timerRef.current)
      timerRef.current = null
    }
  }

  const close = useCallback(() => {
    clearTimer()
    setOpen(false)
  }, [])

  // What the content is handed: by the time anything inside the flyout is used, no open is pending.
  const dismiss = useCallback(() => setOpen(false), [])

  const scheduleOpen = (delay = openDelay) => {
    clearTimer()
    if (!window.matchMedia(desktopQuery).matches) return
    timerRef.current = setTimeout(() => setOpen(true), delay)
  }

  const scheduleClose = () => {
    clearTimer()
    if (interactive) {
      timerRef.current = setTimeout(close, closeDelay)
    } else {
      close()
    }
  }

  // Leaving the rail layout (the sidebar expanded) closes whatever was open, so it is not still open on return.
  if (enabled !== wasEnabled) {
    setWasEnabled(enabled)
    setOpen(false)
  }

  useEffect(() => clearTimer, [])

  // Measure as the flyout attaches, before it paints, so a tall submenu near the bottom is moved up into
  // view. The Transition mounts it in a later commit than the one that opened it, hence a callback ref
  // rather than an effect. The flyout is placed by writing its style directly: it is hidden until then.
  const placeFlyout = useCallback(
    (flyout: HTMLDivElement | null) => {
      flyoutRef.current = flyout
      if (!flyout || !triggerRef.current) return
      const rect = triggerRef.current.getBoundingClientRect()
      const height = flyout.offsetHeight
      const rtl = getComputedStyle(triggerRef.current).direction === 'rtl'
      // A submenu lines its first row up with the entry; a label is centred on it.
      const preferredTop = interactive ? rect.top - 6 : rect.top + (rect.height - height) / 2
      const top = Math.max(viewportMargin, Math.min(preferredTop, window.innerHeight - height - viewportMargin))
      flyout.style.top = `${top}px`
      if (rtl) flyout.style.right = `${window.innerWidth - rect.left + gap}px`
      else flyout.style.left = `${rect.right + gap}px`
      flyout.style.visibility = 'visible'
    },
    [interactive],
  )

  useEffect(() => {
    if (!open) return
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') close()
    }
    // The flyout is pinned to where the entry was; any scroll or resize outside it moves the entry away.
    const onScroll = (event: Event) => {
      if (!flyoutRef.current?.contains(event.target as Node)) close()
    }
    document.addEventListener('keydown', onKeyDown)
    window.addEventListener('scroll', onScroll, true)
    window.addEventListener('resize', close)
    return () => {
      document.removeEventListener('keydown', onKeyDown)
      window.removeEventListener('scroll', onScroll, true)
      window.removeEventListener('resize', close)
    }
  }, [open, close])

  if (!enabled) return <>{children(false)}</>

  return (
    // React carries focus and pointer events from the portaled flyout up to this wrapper, so moving
    // from the entry into its submenu cancels the pending close instead of ending the hover.
    <div
      ref={triggerRef}
      onMouseEnter={() => scheduleOpen()}
      onMouseLeave={scheduleClose}
      onFocus={() => scheduleOpen()}
      onBlur={scheduleClose}
      onClick={openOnClick ? () => scheduleOpen(0) : undefined}
    >
      {children(open)}
      {typeof window !== 'undefined' &&
        createPortal(
          <Transition
            show={open}
            as={Fragment}
            enter="transition-opacity duration-150 ease-out"
            enterFrom="opacity-0"
            enterTo="opacity-100"
            leave="transition-opacity duration-100 ease-in"
            leaveFrom="opacity-100"
            leaveTo="opacity-0"
          >
            <div ref={placeFlyout} className={interactive ? 'invisible fixed z-60' : 'pointer-events-none invisible fixed z-60'} onClick={(event) => event.stopPropagation()}>
              {content(dismiss)}
            </div>
          </Transition>,
          document.body,
        )}
    </div>
  )
}
