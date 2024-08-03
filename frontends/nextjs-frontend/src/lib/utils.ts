import { type ClassValue, clsx } from "clsx"
import { twMerge } from "tailwind-merge"

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export function setLocalStorageValue(key: string, value: any) {
  if (typeof window === "undefined")
    return

  if (value) {
    localStorage.setItem(key, JSON.stringify(value))
  } else {
    localStorage.removeItem(key)
  }
}

export function getLocalStorageValue<T>(key: string, fallbackValue: T | null = null): T | null {
  if (typeof window === "undefined")
    return null

  const stored = localStorage.getItem(key)
  return stored ? JSON.parse(stored) : fallbackValue
}
