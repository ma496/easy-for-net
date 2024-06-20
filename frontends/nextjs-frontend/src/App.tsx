'use client';
import { PropsWithChildren, useEffect, useState } from 'react';
import Loading from '@/components/layout/loading';
import { useAppDispatch, useAppSelector } from './store/hooks';
import { toggleLanguage } from './store/slices/themeConfigSlice';

function App({ children }: PropsWithChildren) {
  const themeConfig = useAppSelector(state => state.themeConfig)
  const dispatch = useAppDispatch()
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    dispatch(toggleLanguage(localStorage.getItem('locale') || themeConfig.locale))

    setIsLoading(false)
  }, [dispatch, themeConfig.locale])

  return (
    <div
      className={`main-section relative font-nunito text-sm font-normal antialiased`}
    >
      {isLoading ? <Loading /> : children}
    </div>
  )
}

export default App
