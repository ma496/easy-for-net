'use client';
import App from '@/App';
import React, { Suspense } from 'react';
import Loading from '@/components/layout/loading';
import StoreProvider from './store-provider';
import ThemesProvider from './themes-provider';
import { ShowError } from '../show-error';

interface IProps {
  children: React.ReactNode
}

const RootProvider = ({ children }: IProps) => {
  return (
    <StoreProvider>
      <ThemesProvider
        defaultTheme="system"
        enableColorScheme
        themes={['light', 'dark-classic', 'rose', 'dark-rose']}
        enableSystem
        disableTransitionOnChange>
        <Suspense fallback={<Loading />}>
          <App>{children}</App>
          <ShowError />
        </Suspense>
      </ThemesProvider>
    </StoreProvider>
  )
}

export default RootProvider
