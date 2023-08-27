import { createSlice, PayloadAction } from "@reduxjs/toolkit"

type SidebarState = {
  open: boolean
  width: number
  headerHeight: number
}

const initialState : SidebarState = {
  open: false,
  width: 256,
  headerHeight: 64
}

export const sidebarSlice = createSlice({
  name: 'sidebar',
  initialState,
  reducers: {
    setOpen: (state, action: PayloadAction<boolean>) => {
      state.open = action.payload
    }
  }
})

export const {
  setOpen
} = sidebarSlice.actions

export default sidebarSlice.reducer
