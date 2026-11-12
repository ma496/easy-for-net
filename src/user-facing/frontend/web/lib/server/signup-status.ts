import { cache } from 'react'
import { isSignupEnabledFrom } from '@/lib/signup-status'
import { serverApiFetch } from './api-fetch'
import { mustRethrow } from './dynamic-usage'

/**
 * Whether the site lets visitors sign up, from the anonymous `GET /account/signup-status`. The answer
 * is the same for every visitor, so no cookie or host is forwarded and it is cached for a minute;
 * memoized per request, so a page and its layout share one fetch. An API that cannot be reached counts
 * as open: the sign-up form still shows, and the endpoint's 403 `signupDisabled` is the fallback.
 */
export const getIsSignupEnabled = cache(async (): Promise<boolean> => {
  try {
    const response = await serverApiFetch('/account/signup-status', { forwardHost: false, next: { revalidate: 60 } })
    return isSignupEnabledFrom(response.ok ? await response.json() : null)
  } catch (error) {
    if (mustRethrow(error)) throw error
    console.error('Failed to load /account/signup-status', error)
    return true
  }
})
