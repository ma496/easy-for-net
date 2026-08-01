'use client'

import React, { useState, useRef, useEffect, useCallback } from 'react'
import { createPortal } from 'react-dom'
import { cn } from '@/lib/utils'
import { Transition } from '@headlessui/react'

/** Allowed placements for the Tooltip relative to its trigger element. */
type TooltipPosition = 'top' | 'bottom' | 'left' | 'right'

/** Props for the Tooltip component, which wraps any element and shows a portal-rendered floating label on hover. */
interface TooltipProps {
  content: React.ReactNode
  children: React.ReactNode
  position?: TooltipPosition
  className?: string
  delay?: number
  animate?: boolean
}

/** Computed absolute coordinates (accounting for scroll) and chosen placement for a Tooltip. */
interface TooltipCoords {
  top: number
  left: number
  position: TooltipPosition
}

/**
 * Tooltip is a client component that displays a floating label with a directional arrow on hover, positioning itself with createPortal and recalculating on scroll and resize.
 * It supports a configurable delay, an optional transition animation, and a placement prop (top/bottom/left/right).
 */
export const Tooltip = ({ content, children, position = 'top', className, delay = 200, animate = false }: TooltipProps) => {
  const [isVisible, setIsVisible] = useState(false)
  const [coords, setCoords] = useState<TooltipCoords | null>(null)
  const triggerRef = useRef<HTMLDivElement>(null)
  const tooltipRef = useRef<HTMLDivElement>(null)
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  const calculatePosition = useCallback((triggerRect: DOMRect, tooltipRect: DOMRect, currentPosition: TooltipPosition): TooltipCoords => {
    let top = 0
    let left = 0

    switch (currentPosition) {
      case 'top':
        top = triggerRect.top - tooltipRect.height - 8
        left = triggerRect.left + (triggerRect.width - tooltipRect.width) / 2
        break
      case 'bottom':
        top = triggerRect.bottom + 8
        left = triggerRect.left + (triggerRect.width - tooltipRect.width) / 2
        break
      case 'left':
        top = triggerRect.top + (triggerRect.height - tooltipRect.height) / 2
        left = triggerRect.left - tooltipRect.width - 8
        break
      case 'right':
        top = triggerRect.top + (triggerRect.height - tooltipRect.height) / 2
        left = triggerRect.right + 8
        break
    }

    return { top: top + window.scrollY, left: left + window.scrollX, position: currentPosition }
  }, [])

  const updatePosition = useCallback(() => {
    if (triggerRef.current && tooltipRef.current && isVisible) {
      const triggerRect = triggerRef.current.getBoundingClientRect()
      const tooltipRect = tooltipRef.current.getBoundingClientRect()

      if (tooltipRect.height === 0) return

      const newCoords = calculatePosition(triggerRect, tooltipRect, position)
      setCoords(newCoords)
    }
  }, [isVisible, position, calculatePosition])

  const setTooltipRef = useCallback((node: HTMLDivElement | null) => {
    tooltipRef.current = node
    if (node) {
      requestAnimationFrame(() => {
        updatePosition()
      })
    }
  }, [updatePosition])

  useEffect(() => {
    if (isVisible) {
      updatePosition()
      window.addEventListener('resize', updatePosition)
      window.addEventListener('scroll', updatePosition, true)
    }
    return () => {
      window.removeEventListener('resize', updatePosition)
      window.removeEventListener('scroll', updatePosition, true)
    }
  }, [isVisible, updatePosition])

  const handleMouseEnter = () => {
    timeoutRef.current = setTimeout(() => setIsVisible(true), delay)
  }

  const handleMouseLeave = () => {
    if (timeoutRef.current) {
      clearTimeout(timeoutRef.current)
    }
    setIsVisible(false)
    setCoords(null)
  }

  return (
    <>
      <div
        ref={triggerRef}
        onMouseEnter={handleMouseEnter}
        onMouseLeave={handleMouseLeave}
        className="inline-block"
      >
        {children}
      </div>
      {typeof window !== 'undefined' &&
        createPortal(
          <Transition
            show={isVisible}
            as={React.Fragment}
            enter={animate ? "transition-all duration-200 ease-out" : ""}
            enterFrom={animate ? "opacity-0 scale-95" : ""}
            enterTo={animate ? "opacity-100 scale-100" : ""}
            leave={animate ? "transition-all duration-150 ease-in" : ""}
            leaveFrom={animate ? "opacity-100 scale-100" : ""}
            leaveTo={animate ? "opacity-0 scale-95" : ""}
          >
            <div
              ref={setTooltipRef}
              style={{
                top: coords?.top ?? 0,
                left: coords?.left ?? 0,
                visibility: coords ? 'visible' : 'hidden',
                position: 'absolute'
              }}
              className={cn(
                'z-9999 pointer-events-none',
                className
              )}
            >
              <div className="relative rounded-lg bg-black/90 backdrop-blur-md px-3 py-1.5 text-xs font-medium text-white shadow-xl dark:bg-gray-800/95 dark:text-white-light">
                {content}
                <div
                  className={cn(
                    'absolute h-2 w-2 rotate-45 border-transparent bg-black/90 dark:bg-gray-800/95',
                    {
                      '-bottom-1 left-1/2 -ml-1': coords?.position === 'top',
                      '-top-1 left-1/2 -ml-1': coords?.position === 'bottom',
                      '-right-1 top-1/2 -mt-1': coords?.position === 'left',
                      '-left-1 top-1/2 -mt-1': coords?.position === 'right'
                    }
                  )}
                />
              </div>
            </div>
          </Transition>,
          document.body
        )}
    </>
  )
}

