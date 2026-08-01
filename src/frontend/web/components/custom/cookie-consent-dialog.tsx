'use client'

import { Transition, TransitionChild } from '@headlessui/react'
import { Fragment } from 'react'
import { Cookie, X } from 'lucide-react'
import { Button } from '@/components/ui'
import { useTranslation } from '@/i18n'

/**
 * Props for the {@link CookieConsentDialog} component, controlling visibility and exposing accept/decline callbacks.
 */
interface CookieConsentDialogProps {
  isOpen: boolean
  onAccept: () => void
  onDecline: () => void
}

/**
 * Renders an animated bottom-sheet dialog prompting the user to accept or decline cookies, with localized title/description/buttons and HeadlessUI transitions.
 */
export const CookieConsentDialog = ({ isOpen, onAccept, onDecline }: CookieConsentDialogProps) => {
  const { t } = useTranslation()

  return (
    <Transition appear show={isOpen} as={Fragment}>
      <div className="fixed inset-0 z-50 flex items-end justify-center">
        {/* Overlay */}
        <TransitionChild
          as={Fragment}
          enter="ease-out duration-300"
          enterFrom="opacity-0"
          enterTo="opacity-100"
          leave="ease-in duration-200"
          leaveFrom="opacity-100"
          leaveTo="opacity-0"
        >
          <div
            className="fixed inset-0 bg-linear-to-t from-black/70 via-black/40 to-transparent backdrop-blur-sm"
            aria-hidden="true"
            onClick={onDecline}
          />
        </TransitionChild>

        {/* Bottom Sheet */}
        <TransitionChild
          as={Fragment}
          enter="ease-out duration-500"
          enterFrom="opacity-0 translate-y-full"
          enterTo="opacity-100 translate-y-0"
          leave="ease-in duration-300"
          leaveFrom="opacity-100 translate-y-0"
          leaveTo="opacity-0 translate-y-full"
        >
          <div className="relative w-full max-w-xl panel bg-white dark:bg-gray-800 rounded-t-2xl md:rounded-2xl shadow-2xl overflow-hidden">
            {/* Decorative gradient header */}
            <div className="relative h-24 bg-linear-to-br from-primary via-secondary to-info overflow-hidden">
              {/* Decorative circles */}
              <div className="absolute -top-6 -right-6 w-28 h-28 bg-white/10 rounded-full blur-xl" />
              <div className="absolute -bottom-4 -left-4 w-20 h-20 bg-white/10 rounded-full blur-lg" />

              {/* Cookie illustration */}
              <div className="absolute inset-0 flex items-center justify-center">
                <div className="relative">
                  <div className="absolute inset-0 bg-white/20 rounded-full blur-2xl scale-150" />
                  <div className="relative w-14 h-14 bg-white rounded-2xl rotate-12 shadow-2xl flex items-center justify-center">
                    <Cookie className="w-7 h-7 text-primary" strokeWidth={2.5} />
                  </div>
                </div>
              </div>

              {/* Close button */}
              <button
                onClick={onDecline}
                type="button"
                className="cursor-pointer absolute top-3 right-3 w-8 h-8 flex items-center justify-center rounded-full bg-white/20 hover:bg-white/30 backdrop-blur-sm text-white transition-all duration-200 hover:scale-110"
                aria-label="Close"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            {/* Content */}
            <div className="p-5 md:p-6 -mt-4 relative">
              <h2 className="text-lg md:text-xl font-bold mb-1.5 text-center bg-linear-to-r from-primary to-secondary bg-clip-text text-transparent">
                {t('cookieConsent.title')}
              </h2>
              <p className="text-center text-sm text-gray-600 dark:text-white-dark/70 mb-5 leading-relaxed">
                {t('cookieConsent.description')}
              </p>

              {/* Action buttons */}
              <div className="flex items-center justify-end gap-2">
                <Button variant="outline-secondary" onClick={onDecline}>
                  {t('cookieConsent.decline')}
                </Button>
                <Button
                  onClick={onAccept}
                  className="bg-linear-to-r from-primary to-secondary hover:shadow-lg transition-shadow duration-200"
                >
                  {t('cookieConsent.accept')}
                </Button>
              </div>
            </div>
          </div>
        </TransitionChild>
      </div>
    </Transition>
  )
}

