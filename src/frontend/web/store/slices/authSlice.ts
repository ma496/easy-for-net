import { createSlice, PayloadAction } from '@reduxjs/toolkit'
import { GetUserInfoResponse } from '../api/identity'
import { AuthState } from '@/lib/utils'

const initialState: AuthState = {
  user: undefined,
  isAuthenticated: false,
  activeTenant: undefined,
  tenants: [],
  tenantError: undefined,
}

/**
 * Auth slice storing the currently signed-in user, an authentication flag, the
 * tenant the user is acting in, every tenant the user holds an active membership
 * in, and the last tenant failure code reported by the API. Provides reducers to
 * set the user info (deriving the auth flag and the tenant fields from it), to
 * record and clear a tenant failure, and to sign out (clearing every field so no
 * tenant selection survives for the next user of the browser).
 */
export const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    setUserInfo(state, { payload }: PayloadAction<GetUserInfoResponse | undefined>) {
      state.user = payload
      state.isAuthenticated = payload !== undefined
      // The tenant fields always come from the freshly fetched user info, so a
      // switch, an onboard or a membership that has been revoked server-side is
      // reflected here rather than leaving a stale selection in place.
      state.activeTenant = payload?.activeTenant
      state.tenants = payload?.tenants ?? []
      state.tenantError = undefined
    },
    setTenantError(state, { payload }: PayloadAction<string>) {
      // Recorded, never signed out: the user stays authenticated and is offered
      // another tenant to work in.
      state.tenantError = payload
    },
    clearTenantError(state) {
      state.tenantError = undefined
    },
    signout(state) {
      state.user = undefined
      state.isAuthenticated = false
      state.activeTenant = undefined
      state.tenants = []
      state.tenantError = undefined
    },
  },
})

export const { setUserInfo, setTenantError, clearTenantError, signout } = authSlice.actions
