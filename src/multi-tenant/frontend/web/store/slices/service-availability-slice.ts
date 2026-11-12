import { createSlice } from '@reduxjs/toolkit'

interface ServiceAvailabilityState {
  isUnavailable: boolean
}

const initialState: ServiceAvailabilityState = {
  isUnavailable: false,
}

/**
 * Tracks whether the backend API is unreachable so the app shell can show
 * an in-place service-unavailable screen without changing the current URL.
 */
export const serviceAvailabilitySlice = createSlice({
  name: 'serviceAvailability',
  initialState,
  reducers: {
    showServiceUnavailable(state) {
      state.isUnavailable = true
    },
    clearServiceUnavailable(state) {
      state.isUnavailable = false
    },
  },
})

export const { showServiceUnavailable, clearServiceUnavailable } = serviceAvailabilitySlice.actions
