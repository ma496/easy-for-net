import { describe, expect, it } from 'vitest'
import type { GetUserInfoResponse, GetUserInfoTenant } from '@/store/api/identity'
import { authSlice, clearTenantError, setTenantError, setUserInfo, signout } from './authSlice'

/** A tenant the caller holds an active membership in, as the account info endpoint reports it. */
const tenant = (id: string): GetUserInfoTenant => ({ id, name: `Tenant ${id}`, identifier: `t-${id}` })

/** The account info the endpoint answers with, over the part the auth state is derived from. */
const userInfo = (overrides: Partial<GetUserInfoResponse> = {}): GetUserInfoResponse => ({
  id: 'user-1',
  username: 'someone',
  email: 'someone@example.com',
  isPlatformAdministrator: false,
  tenants: [],
  roles: [],
  ...overrides,
})

/** The state after the endpoint has answered with that account info. */
const afterSignIn = (info: GetUserInfoResponse) => authSlice.reducer(undefined, setUserInfo(info))

describe('authSlice', () => {
  it('starts with no user, no tenant and no failure', () => {
    expect(authSlice.getInitialState()).toEqual({
      user: undefined,
      isAuthenticated: false,
      activeTenant: undefined,
      tenants: [],
      tenantError: undefined,
    })
  })
})

describe('setUserInfo', () => {
  it('makes the only tenant the active one without the caller having to choose', () => {
    const only = tenant('a')
    const state = afterSignIn(userInfo({ tenants: [only], activeTenantId: only.id, activeTenant: only }))

    expect(state.isAuthenticated).toBe(true)
    expect(state.activeTenant).toEqual(only)
    expect(state.tenants).toEqual([only])
  })

  it('reports every tenant the caller may work in, and no active one until they choose', () => {
    const state = afterSignIn(userInfo({ tenants: [tenant('a'), tenant('b')] }))

    expect(state.isAuthenticated).toBe(true)
    expect(state.tenants).toEqual([tenant('a'), tenant('b')])
    expect(state.activeTenant).toBeUndefined()
  })

  it('takes the active tenant from the account info it was just handed', () => {
    const state = afterSignIn(
      userInfo({ tenants: [tenant('a'), tenant('b')], activeTenantId: 'b', activeTenant: tenant('b') }),
    )

    expect(state.activeTenant).toEqual(tenant('b'))
  })

  it('keeps no tenant and no session when the account info is withdrawn', () => {
    const signedIn = afterSignIn(userInfo({ tenants: [tenant('a')], activeTenant: tenant('a') }))
    const state = authSlice.reducer(signedIn, setUserInfo(undefined))

    expect(state.isAuthenticated).toBe(false)
    expect(state.activeTenant).toBeUndefined()
    expect(state.tenants).toEqual([])
  })

  it('replaces a stale selection with the one the server reports', () => {
    const signedIn = afterSignIn(userInfo({ tenants: [tenant('a')], activeTenant: tenant('a') }))
    const state = authSlice.reducer(
      signedIn,
      setUserInfo(userInfo({ tenants: [tenant('b')], activeTenantId: 'b', activeTenant: tenant('b') })),
    )

    expect(state.activeTenant).toEqual(tenant('b'))
    expect(state.tenants).toEqual([tenant('b')])
  })

  it('clears the recorded tenant failure, the session having been re-established elsewhere', () => {
    const failed = authSlice.reducer(afterSignIn(userInfo({ tenants: [tenant('a')] })), setTenantError('tenantSuspended'))
    const state = authSlice.reducer(failed, setUserInfo(userInfo({ tenants: [tenant('b')], activeTenant: tenant('b') })))

    expect(state.tenantError).toBeUndefined()
  })
})

describe('setTenantError', () => {
  it('records the code the API refused with', () => {
    const state = authSlice.reducer(undefined, setTenantError('tenantSuspended'))

    expect(state.tenantError).toBe('tenantSuspended')
  })

  it('leaves the caller authenticated in the tenant they were acting in', () => {
    const signedIn = afterSignIn(userInfo({ tenants: [tenant('a')], activeTenant: tenant('a') }))
    const state = authSlice.reducer(signedIn, setTenantError('tenantSuspended'))

    expect(state.isAuthenticated).toBe(true)
    expect(state.activeTenant).toEqual(tenant('a'))
    expect(state.tenantError).toBe('tenantSuspended')
  })
})

describe('clearTenantError', () => {
  it('drops the failure and leaves everything else alone', () => {
    const signedIn = afterSignIn(userInfo({ tenants: [tenant('a')], activeTenant: tenant('a') }))
    const failed = authSlice.reducer(signedIn, setTenantError('tenantSuspended'))
    const state = authSlice.reducer(failed, clearTenantError())

    expect(state.tenantError).toBeUndefined()
    expect(state.isAuthenticated).toBe(true)
    expect(state.activeTenant).toEqual(tenant('a'))
  })
})

describe('signout', () => {
  it('leaves no tenant selection for the next user of the browser', () => {
    const signedIn = afterSignIn(userInfo({ tenants: [tenant('a'), tenant('b')], activeTenantId: 'a', activeTenant: tenant('a') }))
    const failed = authSlice.reducer(signedIn, setTenantError('tenantSuspended'))
    const state = authSlice.reducer(failed, signout())

    expect(state).toEqual(authSlice.getInitialState())
  })
})
