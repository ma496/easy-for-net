import { describe, expect, it } from 'vitest'
import type { GetUserInfoTenant } from '@/store/api/identity'
import { isAllowed, type AuthState } from './authentication-and-authorization'

const stateWithPermissions = (...permissions: string[]): AuthState => ({
  isAuthenticated: true,
  activeTenant: undefined,
  tenants: [],
  tenantError: undefined,
  user: {
    id: 'user-id',
    username: 'user',
    email: 'user@example.com',
    tenants: [],
    isPlatform: false,
    roles: [
      {
        id: 'role-id',
        name: 'Admin',
        permissions: permissions.map((name) => ({ id: name, name, displayName: name })),
      },
    ],
  },
})

/** A tenant the caller holds an active membership in, as the account info endpoint reports it. */
const tenant = (id: string): GetUserInfoTenant => ({ id, name: `Tenant ${id}`, identifier: `t-${id}` })

const firstTenant = tenant('first')
const secondTenant = tenant('second')

/**
 * The state of a caller acting in a tenant. The roles in state are the ones the API reports for the
 * tenant being acted in, so this is what `setUserInfo` leaves behind after the account info has been
 * read - the same identity and the same user id whatever tenant it is acting in, with the grants of
 * that tenant alone.
 */
const stateActingIn = (activeTenant: GetUserInfoTenant | undefined, ...permissions: string[]): AuthState => ({
  isAuthenticated: true,
  activeTenant,
  tenants: activeTenant ? [firstTenant, secondTenant] : [],
  tenantError: undefined,
  user: {
    id: 'user-id',
    username: 'user',
    email: 'user@example.com',
    activeTenantId: activeTenant?.id,
    activeTenant,
    tenants: activeTenant ? [firstTenant, secondTenant] : [],
    isPlatform: false,
    roles: [
      {
        id: 'role-id',
        name: 'Admin',
        permissions: permissions.map((name) => ({ id: name, name, displayName: name })),
      },
    ],
  },
})

describe('isAllowed', () => {
  it('requires every requested permission', () => {
    expect(isAllowed(stateWithPermissions('User.View'), ['User.View', 'User.Delete'])).toBe(false)
    expect(isAllowed(stateWithPermissions('User.View', 'User.Delete'), ['User.View', 'User.Delete'])).toBe(true)
  })

  it('allows an authenticated user when no permission is required', () => {
    expect(isAllowed(stateWithPermissions(), [])).toBe(true)
  })

  it('grants a permission the roles of the tenant being acted in carry', () => {
    expect(isAllowed(stateActingIn(firstTenant, 'Tenant.View'), ['Tenant.View'])).toBe(true)
  })

  it('refuses a permission only the other tenant grants', () => {
    const actingInFirst = stateActingIn(firstTenant, 'Tenant.View')
    const actingInSecond = stateActingIn(secondTenant, 'Tenant.View', 'Tenant.Update')

    expect(isAllowed(actingInFirst, ['Tenant.Update'])).toBe(false)
    expect(isAllowed(actingInSecond, ['Tenant.Update'])).toBe(true)

    // The same identity, the same question, and the two tenants answer differently - which is what
    // "evaluated solely from the roles granted in the tenant being acted in" means in practice.
    expect(actingInFirst.user?.id).toBe(actingInSecond.user?.id)
    expect(isAllowed(actingInFirst, ['Tenant.View'])).toBe(isAllowed(actingInSecond, ['Tenant.View']))
  })

  it('refuses a permission no tenant granted, with no tenant to grant it', () => {
    expect(isAllowed(stateActingIn(undefined, 'User.View'), ['Tenant.View'])).toBe(false)
  })

  it('still grants a permission the caller holds while acting in no tenant', () => {
    expect(isAllowed(stateActingIn(undefined, 'Tenant.View'), ['Tenant.View'])).toBe(true)
  })
})
