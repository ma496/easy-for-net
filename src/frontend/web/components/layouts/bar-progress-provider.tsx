'use client'

import { ProgressProvider } from '@bprogress/next/app'

/**
 * BarProgressProvider is a client component that wraps the app with @bprogress/next's top-of-page progress bar, showing a thin purple bar on shallow route transitions.
 */
export const BarProgressProvider = ({ children }: { children: React.ReactNode }) => {
  return (
    <ProgressProvider height="3px" color="#805CCA" options={{ showSpinner: false }} shallowRouting>
      {children}
    </ProgressProvider>
  )
}

