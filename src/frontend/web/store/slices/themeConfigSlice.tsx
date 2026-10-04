import { createSlice } from '@reduxjs/toolkit'
import themeConfig from '@/theme.config'

interface ThemeConfigState {
  isDarkMode: boolean
  /** Desktop: the sidebar is collapsed to its icon rail. Below lg: the sidebar drawer is open. */
  sidebar: boolean
  theme: string
  rtlClass: string
}

const initialState: ThemeConfigState = {
  isDarkMode: false,
  sidebar: false,
  theme: themeConfig.theme,
  rtlClass: themeConfig.rtlClass,
}

/**
 * Theme slice: the chosen color scheme (light, dark or system) and whether it currently resolves to
 * dark, the text direction, and the sidebar's collapsed/open state. Browser persistence and DOM
 * updates are handled by the app component so reducers remain deterministic.
 */
export const themeConfigSlice = createSlice({
  name: 'theme',
  initialState: initialState,
  reducers: {
    toggleTheme(state, { payload }) {
      payload = payload || state.theme // light | dark | system
      state.theme = payload
      if (payload === 'light') {
        state.isDarkMode = false
      } else if (payload === 'dark') {
        state.isDarkMode = true
      }
    },
    setDarkMode(state, { payload }) {
      state.isDarkMode = Boolean(payload)
    },
    toggleRTL(state, { payload }) {
      payload = payload || state.rtlClass // rtl, ltr
      state.rtlClass = payload
    },
    toggleSidebar(state) {
      state.sidebar = !state.sidebar
    },
    setSidebar(state, { payload }) {
      state.sidebar = Boolean(payload)
    },
  },
})

export const { toggleTheme, setDarkMode, toggleRTL, toggleSidebar, setSidebar } = themeConfigSlice.actions
