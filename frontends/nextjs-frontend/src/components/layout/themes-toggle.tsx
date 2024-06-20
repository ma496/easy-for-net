"use client"

import { Moon, Sun } from "lucide-react"
import { useTheme } from "next-themes"

import { Button } from "@/components/ui/button"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { useEffect, useState } from "react"
import { useTranslations } from "next-intl"

export function ThemesToggle() {
  const { theme, setTheme } = useTheme()
  const [mounted, setMounted] = useState(false)
  const t = useTranslations()
  // The active theme is not available on the server.
  // If you have styling that is conditionally applied based on the active-theme,
  // you have to await the mounted state before rendering the active theme.
  useEffect(() => setMounted(true), [])

  const themeMapping: Record<string, string> = {
    'light': t('Light'),
    'dark-classic': t('Dark'),
    'rose': t('Rose'),
    'dark-rose': `${t('Rose')} (${t('Dark')})`,
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="outline" size="icon" className="rounded-full">
          <Sun className="h-[1.2rem] w-[1.2rem] rotate-0 scale-100 transition-all dark:-rotate-90 dark:scale-0" />
          <Moon className="absolute h-[1.2rem] w-[1.2rem] rotate-90 scale-0 transition-all dark:rotate-0 dark:scale-100" />
          <span className="sr-only">Toggle theme</span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <div className="flex flex-wrap gap-2 justify-around">
          {
            Object.entries(themeMapping).map(([key, value]) => (
              <DropdownMenuItem
                key={key}
                onClick={() => setTheme(key)}
                className={`font-semibold rounded-md transition-colors duration-200 justify-center basis-[47%] ${
                  // The theme is only available after the component is mounted.
                  mounted && theme == key
                    ? 'border border-primary bg-primary-foreground text-primary'
                    : 'bg-primary text-primary-foreground'
                  }`}
              >
                <span className="capitalize">{value}</span>
              </DropdownMenuItem>
            ))
          }
        </div>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
