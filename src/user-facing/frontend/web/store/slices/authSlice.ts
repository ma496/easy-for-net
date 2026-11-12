import { createSlice, PayloadAction } from '@reduxjs/toolkit'
import { GetUserInfoResponse } from '../api/identity'
import { AuthState } from '@/lib/utils'

/** The auth slice: the signed-in user, and whether this document's first read of the account info has answered. */
export interface AuthSliceState extends AuthState {
  /**
   * Whether this document's own first read of the account info has answered (with a user or without).
   * The guarded areas wait on it before deciding anything; the public site never does.
   */
  isSettled: boolean
}

const initialState: AuthSliceState = {
  user: undefined,
  isAuthenticated: false,
  isSettled: false,
}

/**
 * Auth slice storing the currently signed-in user and an authentication flag. Provides reducers to set
 * the user info (deriving the auth flag from it), to record that the first read has answered, and to
 * sign out, clearing the user so nothing of the previous account survives for the next user of the browser.
 */
export const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    setUserInfo(state, { payload }: PayloadAction<GetUserInfoResponse | undefined>) {
      state.user = payload
      state.isAuthenticated = payload !== undefined
    },
    markUserInfoSettled(state) {
      state.isSettled = true
    },
    signout(state) {
      state.user = undefined
      state.isAuthenticated = false
    },
  },
})

export const { setUserInfo, markUserInfoSettled, signout } = authSlice.actions
