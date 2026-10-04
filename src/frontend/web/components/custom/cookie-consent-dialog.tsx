'use client'

import { Transition } from '@headlessui/react'
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
 * Renders a floating consent card at the bottom of the viewport asking the user to accept or decline cookies, with localized title/description/buttons. It does not block the page; closing it counts as declining.
 */
export const CookieConsentDialog = ({ isOpen, onAccept, onDecline }: CookieConsentDialogProps) => {
  const { t } = useTranslation()

  return (
    <Transition
      appear
      show={isOpen}
      enter="ease-out duration-300"
      enterFrom="opacity-0 translate-y-4"
      enterTo="opacity-100 translate-y-0"
      leave="ease-in duration-200"
      leaveFrom="opacity-100 translate-y-0"
      leaveTo="opacity-0 translate-y-4"
    >
      <div role="dialog" aria-labelledby="cookie-consent-title" className="fixed inset-x-3 bottom-3 z-[80] sm:inset-x-auto sm:start-6 sm:bottom-6 sm:w-[26rem]">
        <div className="relative rounded-2xl border border-border bg-surface p-5 shadow-lg">
          <button onClick={onDecline} type="button" className="icon-btn absolute end-3 top-3 size-7" aria-label={t('common.close')}>
            <X size={15} />
          </button>
          <div className="flex gap-4">
            <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary">
              <Cookie size={20} />
            </span>
            <div className="min-w-0 pe-6">
              <h2 id="cookie-consent-title" className="text-sm font-semibold text-foreground">
                {t('cookieConsent.title')}
              </h2>
              <p className="mt-1 text-[13px] leading-relaxed text-muted-foreground">{t('cookieConsent.description')}</p>
            </div>
          </div>
          <div className="mt-4 flex items-center justify-end gap-2">
            <Button variant="outline" size="sm" onClick={onDecline}>
              {t('cookieConsent.decline')}
            </Button>
            <Button size="sm" onClick={onAccept}>
              {t('cookieConsent.accept')}
            </Button>
          </div>
        </div>
      </div>
    </Transition>
  )
}
