'use client'

import { useState } from 'react'
import { RefreshCw, ServerOff } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { Button } from '@/components/ui'

/**
 * Full-page view displayed in place of the current route when the backend API
 * is unreachable. Retrying reloads the current URL and rebuilds normal app state.
 */
export const ServiceUnavailableView = () => {
  const { t } = useTranslation()
  const [isRetrying, setIsRetrying] = useState(false)

  const retry = () => {
    setIsRetrying(true)
    window.location.reload()
  }

  return (
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden bg-white dark:bg-[#060818]">
      <div className="pointer-events-none absolute top-1/2 left-1/2 aspect-square w-full max-w-4xl -translate-x-1/2 -translate-y-1/2 bg-[radial-gradient(circle,#4361EE_0%,transparent_70%)] opacity-[0.07]" />

      <div className="relative z-10 px-6 py-16 text-center font-nunito">
        <div className="relative flex flex-col items-center">
          <div className="group relative mb-12">
            <div className="bg-warning/20 absolute inset-0 scale-150 animate-pulse rounded-full blur-3xl" />
            <div className="bg-primary/10 absolute inset-0 -rotate-12 scale-125 rounded-full blur-2xl transition-transform duration-1000 group-hover:rotate-12" />
            <div className="relative rounded-[3rem] border border-white/20 bg-white p-10 shadow-2xl backdrop-blur-xl transition-transform duration-500 group-hover:scale-105 md:p-14 dark:bg-black/20">
              <ServerOff className="text-warning h-24 w-24 md:h-32 md:w-32" strokeWidth={1.2} />
            </div>
            <div className="bg-warning absolute -top-6 -right-6 rotate-12 transform cursor-default select-none rounded-2xl border-4 border-white px-6 py-3 text-2xl font-black text-white shadow-2xl transition-transform duration-500 hover:scale-110 hover:rotate-0 md:text-3xl dark:border-[#060818]">
              503
            </div>
          </div>

          <div className="mx-auto max-w-md space-y-6">
            <h1 className="text-5xl font-black tracking-tighter uppercase md:text-6xl dark:text-white">{t('error.serviceUnavailable.title')}</h1>
            <p className="text-lg leading-relaxed font-medium text-gray-500 md:text-xl dark:text-gray-400">{t('error.serviceUnavailable.message')}</p>
            <Button type="button" size="lg" rounded="full" icon={<RefreshCw className="h-5 w-5" />} isLoading={isRetrying} onClick={retry}>
              {t('error.serviceUnavailable.retry')}
            </Button>
          </div>
        </div>
      </div>

      <div
        className="pointer-events-none absolute inset-0 opacity-[0.1]"
        style={{ backgroundImage: 'radial-gradient(#808080 1px, transparent 1px)', backgroundSize: '30px 30px' }}
      />
    </div>
  )
}
