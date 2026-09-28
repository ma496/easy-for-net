'use client'
import { useMyFeaturesQuery } from '@/store/api/tenancy'
import { useAppSelector } from '@/store/hooks'
import { FeatureNames } from '@/feature-names'
import { planMegabytesToBytes } from '@/lib/utils/upload-limit'

/**
 * The largest file, in bytes, the caller's plan lets them upload, so an upload component can refuse an
 * oversize file before sending it.
 *
 * The API holds every upload made inside a tenant to that tenant's `FileManagement.MaxFileSizeMb`, and
 * an upload made in platform scope to no plan at all. This mirrors that: with no active tenant the
 * plan is not asked, and there is no limit beyond whatever the component itself sets.
 *
 * @returns The limit in bytes, or `undefined` when none applies or it has not been read yet.
 */
export const usePlanMaxUploadBytes = (): number | undefined => {
  const hasActiveTenant = useAppSelector((state) => state.auth.activeTenant !== undefined)
  const { data } = useMyFeaturesQuery(undefined, { skip: !hasActiveTenant })

  return hasActiveTenant ? planMegabytesToBytes(data?.features?.[FeatureNames.FileManagement_MaxFileSizeMb]) : undefined
}
