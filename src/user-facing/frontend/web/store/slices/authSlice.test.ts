import { describe, expect, it } from 'vitest'
import type { GetUserInfoResponse } from '@/store/api/identity'
import { authSlice, markUserInfoSettled, setUserInfo, signout } from './authSlice'

/** The account info the endpoint answers with. */
const userInfo = (overrides: Partial<GetUserInfoResponse> = {}): GetUserInfoResponse => ({
  id: 'user-1',
  username: 'someone',
  email: 'someone@example.com',
  roles: [],
  ...overrides,
})

describe('authSlice', () => {
  it('starts with no user', () => {
    expect(authSlice.getInitialState()).toEqual({ user: undefined, isAuthenticated: false, isSettled: false })
  })

  it('is authenticated once the account info arrives', () => {
    const state = authSlice.reducer(undefined, setUserInfo(userInfo()))

    expect(state.isAuthenticated).toBe(true)
    expect(state.user?.username).toBe('someone')
  })

  it('keeps no session when the account info is withdrawn', () => {
    const signedIn = authSlice.reducer(undefined, setUserInfo(userInfo()))
    const state = authSlice.reducer(signedIn, setUserInfo(undefined))

    expect(state.isAuthenticated).toBe(false)
    expect(state.user).toBeUndefined()
  })

  it('records that the first read has answered, signed in or not', () => {
    expect(authSlice.reducer(undefined, markUserInfoSettled()).isSettled).toBe(true)
  })

  it('leaves nothing of the account for the next user of the browser on sign-out', () => {
    const signedIn = authSlice.reducer(undefined, setUserInfo(userInfo()))

    const state = authSlice.reducer(signedIn, signout())

    expect(state.user).toBeUndefined()
    expect(state.isAuthenticated).toBe(false)
  })
})
