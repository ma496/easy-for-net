import { type ClassValue, clsx } from "clsx"
import { twMerge } from "tailwind-merge"

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export function appendUrl(url1: string, url2: string): string {
  let url = `/${url1}/${url2}`
  return url.replace(/\/+/g, '/')
}
