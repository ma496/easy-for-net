import { createSlice, PayloadAction } from "@reduxjs/toolkit"

type ErrorState = {
  isError: boolean
  message: string | null
  title: string | null
}

const initialState: ErrorState = {
  isError: false,
  message: null,
  title: null
}

export const errorSlice = createSlice({
  name: 'error',
  initialState,
  reducers: {
    setError: (state, action: PayloadAction<string | { message: string, title: string }>) => {
      state.isError = true
      if (typeof action.payload === 'string') {
        state.message = action.payload
        state.title = 'Error'
      } else {
        state.message = action.payload.message
        state.title = action.payload.title
      }
    },
    clearError: state => {
      state.isError = false
      state.message = null
      state.title = null
    }
  }
})

export const {
  setError,
  clearError
} = errorSlice.actions
