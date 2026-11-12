'use client'
import App from '@/App'
import { store } from '@/store'
import { Provider } from 'react-redux'
import { ReactNode, Suspense } from 'react'
import { NuqsAdapter } from 'nuqs/adapters/next/app'
import { AppLoading } from './app-loading'
import { BarProgressProvider } from './bar-progress-provider'

/**
 * Props for the {@link ProviderComponent}, accepting the children to wrap with the global providers.
 */
interface IProps {
  children?: ReactNode
}

/**
 * Top-level provider tree that composes the Redux store, Nuqs URL-state adapter, the bar-progress provider, and a Suspense boundary (with the app loading fallback) around the root {@link App} component.
 */
export const ProviderComponent = ({ children }: IProps) => {
  return (
    <Provider store={store}>
      <NuqsAdapter>
        <BarProgressProvider>
          <Suspense fallback={<AppLoading />}>
            <App>{children}</App>
          </Suspense>
        </BarProgressProvider>
      </NuqsAdapter>
    </Provider>
  )
}

