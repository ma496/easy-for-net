'use client'

import { useCallback, useSyncExternalStore } from 'react'

/**
 * Whether the given CSS media query currently matches, kept in step as the viewport changes.
 * On the server, and during hydration, it reports `false`.
 */
export function useMediaQuery(query: string): boolean {
  const subscribe = useCallback(
    (onChange: () => void) => {
      const list = window.matchMedia(query)
      list.addEventListener('change', onChange)
      return () => list.removeEventListener('change', onChange)
    },
    [query],
  )

  return useSyncExternalStore(
    subscribe,
    () => window.matchMedia(query).matches,
    () => false,
  )
}
