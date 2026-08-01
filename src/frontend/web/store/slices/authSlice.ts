import { createSlice, PayloadAction } from '@reduxjs/toolkit'
import { GetUserInfoResponse } from '../api/identity'
import { AuthState } from '@/lib/utils'

const initialState: AuthState = {
  user: undefined,
  isAuthenticated: false,
}

/**
 * Auth slice storing the currently signed-in user and an authentication
 * flag. Provides reducers to set the user info (and derive the auth flag)
 * and to sign out (clearing both fields).
 */
export const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    setUserInfo(state, { payload }: PayloadAction<GetUserInfoResponse | undefined>) {
      state.user = payload
      state.isAuthenticated = payload !== undefined
    },
    signout(state) {
      state.user = undefined
      state.isAuthenticated = false
    },
  },
})

export const { setUserInfo, signout } = authSlice.actions
