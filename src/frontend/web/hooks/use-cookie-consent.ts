'use client'

import { useState, useEffect } from 'react'
import { getLocalStorageValue, setLocalStorageValue } from '@/lib/utils'

const COOKIE_CONSENT_KEY = 'cookie_consent'

interface CookieConsentData {
  consented: boolean
  timestamp: number
}

/**
 * Manages the cookie consent dialog state by reading/writing the user's
 * consent decision to localStorage. The dialog is re-shown once 24 hours
 * have passed since the last interaction. Returns visibility flags plus
 * accept and decline handlers.
 */
export const useCookieConsent = () => {
  const [showDialog, setShowDialog] = useState(false)
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    const checkConsent = () => {
      const stored = getLocalStorageValue<CookieConsentData>(COOKIE_CONSENT_KEY)

      if (!stored) {
        setShowDialog(true)
        setIsLoading(false)
        return
      }

      // Check if 24 hours have passed since last interaction
      const hoursSinceConsent = (Date.now() - stored.timestamp) / (1000 * 60 * 60)

      if (hoursSinceConsent >= 24) {
        setShowDialog(true)
      } else {
        setShowDialog(false)
      }
      setIsLoading(false)
    }

    checkConsent()
  }, [])

  const accept = () => {
    setLocalStorageValue(COOKIE_CONSENT_KEY, {
      consented: true,
      timestamp: Date.now(),
    })
    setShowDialog(false)
  }

  const decline = () => {
    setLocalStorageValue(COOKIE_CONSENT_KEY, {
      consented: false,
      timestamp: Date.now(),
    })
    setShowDialog(false)
  }

  return { showDialog, isLoading, accept, decline }
}
