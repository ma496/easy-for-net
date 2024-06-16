'use client';
import { PropsWithChildren, useEffect, useState } from 'react';
import Loading from '@/components/layout/loading';
import { useAppDispatch, useAppSelector } from './store/hooks';

function App({ children }: PropsWithChildren) {
  const themeConfig = useAppSelector(state => state.themeConfig)
  const dispatch = useAppDispatch()
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {

    setIsLoading(false)
  }, [dispatch, ])

  return (
    <div
      className={`main-section relative font-nunito text-sm font-normal antialiased`}
    >
      {isLoading ? <Loading /> : children}
    </div>
  )
}

export default App
