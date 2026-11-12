'use client'

import { createContext, useEffect } from 'react'
import { emptyLocalizationResources, type LocalizationResourcesResponse } from '@/i18n'

export const TranslationContext = createContext<LocalizationResourcesResponse>(emptyLocalizationResources('en'))

export let globalDictionary: LocalizationResourcesResponse = emptyLocalizationResources('en')

/**
 * Context provider that publishes the current locale's merged localization resources (the flat
 * dictionary, the served culture, the acting scope's default culture and its enabled languages) to
 * descendants, and mirrors the same value into a module-level {@link globalDictionary} reference
 * inside an effect so non-React code can read translations too.
 */
export const TranslationProvider = ({ dictionary, children }: { dictionary: LocalizationResourcesResponse; children: React.ReactNode }) => {
  useEffect(() => {
    globalDictionary = dictionary
  }, [dictionary])
  return <TranslationContext.Provider value={dictionary}>{children}</TranslationContext.Provider>
}
