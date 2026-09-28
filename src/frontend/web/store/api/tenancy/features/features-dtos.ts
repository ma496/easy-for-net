import { RequestBase } from '@/store/api'

/**
 * The providers whose feature values are stored and can therefore be edited. Configuration and the
 * declared defaults are read-only fallbacks and belong to nobody, so they never appear here.
 */
export const FeatureValueProvider = {
  Tenant: 'Tenant',
  Edition: 'Edition',
} as const

/** A provider whose feature values can be edited. */
export type FeatureValueProviderName = (typeof FeatureValueProvider)[keyof typeof FeatureValueProvider]

/** The kinds of value a feature can hold, which decide the control the editor renders. */
export const FeatureValueTypeName = {
  Toggle: 'Toggle',
  FreeText: 'FreeText',
  Selection: 'Selection',
} as const

/** Request parameters for reading the feature catalogue for one tenant or one edition. */
export interface FeatureValueGetRequest extends RequestBase {
  providerName: FeatureValueProviderName
  providerKey: string
}

/** The whole catalogue with each feature's effective value, grouped as the editor lists it. */
export interface FeatureValueGetResponse {
  groups: FeatureGroupDto[]
}

/** One group of features as the editor lists them. */
export interface FeatureGroupDto {
  groupName: string
  displayName: string
  features: FeatureDto[]
}

/** One feature, its effective value, and where that value came from. */
export interface FeatureDto {
  name: string
  displayName: string
  description?: string | null
  /** The feature this one refines, or null for a root feature. */
  parentName?: string | null
  /** How far to indent the row, counted from its root. */
  depth: number
  /** The value in force, whoever supplied it. */
  value?: string | null
  /** Which provider supplied it, so the editor can say what a value is inherited from. */
  providerName?: string | null
  /** Whether the provider being edited set this value itself, and so has one to clear. */
  isOverridden: boolean
  /** Whether the feature is in force — its own value reads as true and so does every toggle above it. */
  isEnabled: boolean
  valueType: FeatureValueTypeDto
}

/** What kind of value a feature holds, and what the editor may accept for it. */
export interface FeatureValueTypeDto {
  name: string
  validatorName: string
  /** The validator's parameters — bounds, lengths, patterns — so the editor can constrain the input. */
  validatorProperties: Record<string, string>
  /** The options on offer, for a selection feature. Empty for every other kind. */
  items: FeatureSelectionItemDto[]
}

/** One option of a selection feature. */
export interface FeatureSelectionItemDto {
  value: string
  displayName: string
}

/** Request body setting what one tenant or one edition is entitled to. */
export interface FeatureValueUpdateRequest extends RequestBase {
  providerName: FeatureValueProviderName
  providerKey: string
  features: FeatureValueDto[]
}

/** One feature and the value it is being set to, or null to clear the override and inherit again. */
export interface FeatureValueDto {
  name: string
  value?: string | null
}

/** Response from the update-features endpoint, reporting how many features the request touched. */
export interface FeatureValueUpdateResponse {
  changed: number
}

/** The caller's own plan: every client-visible feature's value, and whether it is in force. */
export interface MyFeaturesResponse {
  features: Record<string, string | null>
  enabled: Record<string, boolean>
}
