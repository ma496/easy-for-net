'use client'
import { useMyFeaturesQuery } from '@/store/api/tenancy'
import type { FeatureName } from '@/feature-names'

/** What `useFeature` reports about one feature of the caller's own plan. */
export interface FeatureState {
  /** Whether the feature is in force — its own value and every toggle above it read as enabled. */
  isEnabled: boolean
  /** The effective value, for a feature that holds more than a yes or no. */
  value: string | null
  /** True until the caller's plan has been read. */
  isLoading: boolean
}

/**
 * Reports whether the caller's own plan includes a feature.
 *
 * Most screens should not need this. A permission gated on a feature is simply absent from the
 * session when the plan withholds it, so the existing `isAllowed` check already hides the action —
 * reach for this only where there is no permission to gate on, such as a numeric limit, or a panel
 * that should offer an upgrade rather than disappear.
 *
 * @param name The feature to ask about.
 * @returns Whether it is in force, its effective value, and whether the answer has arrived yet.
 */
export const useFeature = (name: FeatureName): FeatureState => {
  const { data, isLoading } = useMyFeaturesQuery()

  return {
    isEnabled: data?.enabled?.[name] ?? false,
    value: data?.features?.[name] ?? null,
    isLoading,
  }
}
