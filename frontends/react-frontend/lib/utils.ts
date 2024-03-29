import { type ClassValue, clsx } from "clsx"
import { twMerge } from "tailwind-merge"

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export function appendUrl(url1: string, url2: string): string {
  let url = `/${url1}/${url2}`
  return url.replace(/\/+/g, '/')
}

export function setLocalStorageValue(key: string, value: any) {
  if (value) {
    localStorage.setItem(key, JSON.stringify(value))
  } else {
    localStorage.removeItem(key)
  }
}

export function getLocalStorageValue<T>(key: string, fallbackValue: T | null = null): T | null {
  const stored = localStorage.getItem(key)
  return stored ? JSON.parse(stored) : fallbackValue
}
