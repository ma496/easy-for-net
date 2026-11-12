'use client'

import { useState, useEffect } from 'react'

/**
 * Returns a debounced version of the given value that only updates after the
 * specified delay has elapsed since the last change. Useful for throttling
 * expensive effects (e.g. network requests) triggered by rapid input changes.
 */
export function useDebounce<T>(value: T, delay: number): T {
  const [debouncedValue, setDebouncedValue] = useState<T>(value)

  useEffect(() => {
    const handler = setTimeout(() => {
      setDebouncedValue(value)
    }, delay)

    return () => {
      clearTimeout(handler)
    }
  }, [value, delay])

  return debouncedValue
}
