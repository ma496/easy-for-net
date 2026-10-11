import { type ClassValue, clsx } from 'clsx'
import { twMerge } from 'tailwind-merge'
import { getTranslation } from '@/i18n'

/**
 * Merges Tailwind class values, applying clsx for conditional class joining
 * and tailwind-merge so that later utility classes override conflicting ones.
 */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

/**
 * Persists a value to localStorage under the given key as JSON, or removes
 * the key when the value is falsy. No-op during server-side rendering.
 */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export function setLocalStorageValue(key: string, value: any) {
  if (typeof window === 'undefined') return

  if (value) {
    localStorage.setItem(key, JSON.stringify(value))
  } else {
    localStorage.removeItem(key)
  }
}

/**
 * Reads a JSON-encoded value from localStorage and parses it. Returns the
 * supplied fallback (or undefined) when running server-side or when the
 * key is missing.
 */
export function getLocalStorageValue<T>(key: string, fallbackValue: T | undefined = undefined): T | undefined {
  if (typeof window === 'undefined') return undefined

  const stored = localStorage.getItem(key)
  return stored ? JSON.parse(stored) : fallbackValue
}

/**
 * Builds a short (up to two characters) uppercase avatar label from the
 * user's full name (first letters of the first two words) or, as a
 * fallback, the first two letters of the username.
 */
export const shortName = (name: string | undefined, username: string | undefined) => {
  const shortName =
    name && name.length >= 2
      ? name
        .split(' ')
        .slice(0, 2)
        .map((n) => n[0])
        .join('')
      : username && username.length >= 2
        ? username.slice(0, 2)
        : ''
  return shortName.toUpperCase()
}

/**
 * Returns true if the given i18n key resolves to a translated string
 * (i.e. the translator returns a non-empty value for it),
 * allowing callers to safely fall back when a key is missing.
 */
export const isTranslationKeyExist = (key: string) => {
  const { t } = getTranslation()
  return t(key) !== ''
}
