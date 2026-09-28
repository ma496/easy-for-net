import { createSlice, PayloadAction } from '@reduxjs/toolkit'
import { GetUserInfoResponse } from '../api/identity'
import { AuthState } from '@/lib/utils'

const initialState: AuthState = {
  user: undefined,
  isAuthenticated: false,
  activeTenant: undefined,
  tenants: [],
}

/**
 * Auth slice storing the currently signed-in user, an authentication flag, the
 * tenant the user is acting in, and every tenant the user holds an active
 * membership in. Provides reducers to set the user info (deriving the auth flag
 * and the tenant fields from it) and to sign out, clearing every field so no
 * tenant selection survives for the next user of the browser.
 */
export const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    setUserInfo(state, { payload }: PayloadAction<GetUserInfoResponse | undefined>) {
      state.user = payload
      state.isAuthenticated = payload !== undefined
      // The tenant fields always come from the freshly fetched user info, so a
      // switch, or a tenant the session lost when it was last renewed, is
      // reflected here rather than leaving a stale selection in place.
      state.activeTenant = payload?.activeTenant
      state.tenants = payload?.tenants ?? []
    },
    signout(state) {
      state.user = undefined
      state.isAuthenticated = false
      state.activeTenant = undefined
      state.tenants = []
    },
  },
})

export const { setUserInfo, signout } = authSlice.actions
