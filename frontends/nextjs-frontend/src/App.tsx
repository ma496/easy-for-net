'use client';
import { PropsWithChildren, useEffect, useState } from 'react';
import Loading from '@/components/layout/loading';
import { useAppDispatch, useAppSelector } from './store/hooks';
import { toggleLanguage } from './store/slices/themeConfigSlice';
import { useLocale } from 'next-intl';

function App({ children }: PropsWithChildren) {
  const themeConfig = useAppSelector(state => state.themeConfig)
  const dispatch = useAppDispatch()
  const locale = useLocale()
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    dispatch(toggleLanguage(locale || themeConfig.locale))

    setIsLoading(false)
  }, [locale, themeConfig.locale])

  return (
    <div
      className={`main-section relative font-nunito text-sm font-normal antialiased`}
    >
      {isLoading ? <Loading /> : children}
    </div>
  )
}

export default App
