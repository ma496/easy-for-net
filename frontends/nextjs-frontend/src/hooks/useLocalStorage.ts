import { useEffect, useState } from "react"
import { getLocalStorageValue, setLocalStorageValue } from "../utils"

export function useLocalStorage<T>(key: string, fallbackValue: T | null = null) {
  const [value, setValue] = useState<T | null>(getLocalStorageValue(key, fallbackValue))

  useEffect(() => {
    setLocalStorageValue(key, value)
  }, [value])

  return [value, setValue] as const
}
