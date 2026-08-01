'use client'

import { useLocalizedRouter } from '@/hooks/use-localized-router'
import { useTranslation } from '@/i18n'
import { ArrowLeft, ShieldAlert } from 'lucide-react'
import { Button } from '@/components/ui'

/**
 * Interactive client-side view that displays the 403 access-denied illustration, localized title/message, and a back button.
 */
export const UnauthorizedView = () => {
  const router = useLocalizedRouter()
  const { t } = useTranslation()

  const handleBack = () => {
    router.back()
  }

  return (
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden bg-white dark:bg-[#060818]">
      {/* Premium Background Elements */}
      <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-full max-w-4xl aspect-square bg-[radial-gradient(circle,#4361EE_0%,transparent_70%)] opacity-[0.07] pointer-events-none" />

      <div className="relative z-10 px-6 py-16 text-center font-nunito">
        <div className="relative flex flex-col items-center">
          {/* Main Visual Illustration */}
          <div className="relative mb-12 group">
            <div className="absolute inset-0 bg-danger/20 blur-3xl rounded-full scale-150 animate-pulse" />
            <div className="absolute inset-0 bg-primary/10 blur-2xl rounded-full scale-125 -rotate-12 group-hover:rotate-12 transition-transform duration-1000" />
            <div className="relative bg-white dark:bg-black/20 p-10 md:p-14 rounded-[3rem] border border-white/20 shadow-2xl backdrop-blur-xl transition-transform duration-500 group-hover:scale-105">
              <ShieldAlert className="w-24 h-24 md:w-32 md:h-32 text-danger" strokeWidth={1.2} />
            </div>
            <div className="absolute -top-6 -right-6 bg-danger text-white font-black text-2xl md:text-3xl px-6 py-3 rounded-2xl shadow-2xl rotate-12 border-4 border-white dark:border-[#060818] transform transition-transform duration-500 hover:rotate-0 hover:scale-110 cursor-default select-none">
              403
            </div>
          </div>

          <div className="max-w-md mx-auto space-y-6">
            <h1 className="text-5xl md:text-6xl font-black dark:text-white tracking-tighter uppercase">
              {t('error.403.title')}
            </h1>
            <p className="text-lg md:text-xl text-gray-500 dark:text-gray-400 leading-relaxed font-medium">
              {t('error.403.message')}
            </p>
          </div>

          <Button
            onClick={handleBack}
            className="btn mx-auto mt-7! w-max border-0 btn-gradient uppercase shadow-none flex items-center gap-2 group"
          >
            <ArrowLeft className="w-6 h-6 transition-transform group-hover:-translate-x-2" />
            {t('common.back')}
          </Button>
        </div>
      </div>

      <div className="absolute inset-0 opacity-[0.1] pointer-events-none"
        style={{ backgroundImage: 'radial-gradient(#808080 1px, transparent 1px)', backgroundSize: '30px 30px' }} />
    </div>
  )
}

