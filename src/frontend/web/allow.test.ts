import { describe, expect, it } from 'vitest'
import { Allow } from './allow'

/**
 * The tenancy permissions of the API's `Allow` catalog, restated here as the client must name them.
 * The pair is only useful if the two sides agree: a permission renamed on one side alone is either a
 * screen nobody can open or a gate that never closes, and neither shows up as a compile error.
 */
const tenancyPermissions = {
  Tenant_View: 'Tenant.View',
  Tenant_Detail: 'Tenant.Detail',
  Tenant_Create: 'Tenant.Create',
  Tenant_Update: 'Tenant.Update',
  Tenant_Suspend: 'Tenant.Suspend',
  Tenant_Reactivate: 'Tenant.Reactivate',
  Tenant_Delete: 'Tenant.Delete',
  TenantMember_View: 'TenantMember.View',
  TenantMember_Add: 'TenantMember.Add',
  TenantMember_UpdateRoles: 'TenantMember.UpdateRoles',
  TenantMember_Remove: 'TenantMember.Remove',
} as const

/**
 * The entitlement permissions, restated the same way. All of them are platform-scoped: administering
 * what a plan includes is an act on the platform, never something a tenant does to itself.
 */
const entitlementPermissions = {
  Edition_View: 'Edition.View',
  Edition_Create: 'Edition.Create',
  Edition_Update: 'Edition.Update',
  Edition_Delete: 'Edition.Delete',
  FeatureValue_View: 'FeatureValue.View',
  FeatureValue_Manage: 'FeatureValue.Manage',
} as const

describe('Allow', () => {
  it.each(Object.entries(tenancyPermissions))('mirrors %s as %s', (name, value) => {
    expect(Allow[name as keyof typeof tenancyPermissions]).toBe(value)
  })

  it('carries every tenancy permission the API enforces, and no others', () => {
    const tenancyNames = Object.keys(tenancyPermissions).sort()
    const carried = Object.keys(Allow)
      .filter((name) => name.startsWith('Tenant'))
      .sort()

    expect(carried).toEqual(tenancyNames)
  })

  it.each(Object.entries(entitlementPermissions))('mirrors %s as %s', (name, value) => {
    expect(Allow[name as keyof typeof entitlementPermissions]).toBe(value)
  })

  it('carries every entitlement permission the API enforces, and no others', () => {
    const entitlementNames = Object.keys(entitlementPermissions).sort()
    const carried = Object.keys(Allow)
      .filter((name) => name.startsWith('Edition') || name.startsWith('FeatureValue'))
      .sort()

    expect(carried).toEqual(entitlementNames)
  })

  it('gives no two permissions the same value', () => {
    const values = Object.values(Allow)

    expect(new Set(values).size).toBe(values.length)
  })

  it('names every permission as the group and the operation it belongs to', () => {
    for (const value of Object.values(Allow)) {
      expect(value).toMatch(/^[A-Z][A-Za-z]*\.[A-Z][A-Za-z]*$/)
    }
  })
})
