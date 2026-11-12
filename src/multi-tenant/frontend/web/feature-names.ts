/**
 * Central registry of feature (entitlement) keys — what a tenant's plan either includes or withholds.
 * Every entry mirrors a constant of the same name in the API's `FeatureNames` catalog.
 *
 * Features are not permissions. A permission answers whether this caller may do something; a feature
 * answers whether the tenant's plan covers it at all, which is the same answer for everyone in the
 * tenant. A permission gated on a feature is simply absent from the session, so most screens never
 * need to consult this — reach for it only where there is no permission to gate on, such as a numeric
 * limit or an upsell.
 */
export const FeatureNames = {
  Identity_UserManagement: 'Identity.UserManagement',
  Identity_MaxUserCount: 'Identity.MaxUserCount',

  FileManagement_Enabled: 'FileManagement.Enabled',
  FileManagement_MaxFileSizeMb: 'FileManagement.MaxFileSizeMb',
} as const

/** The name of any feature declared in the catalog above. */
export type FeatureName = (typeof FeatureNames)[keyof typeof FeatureNames]
