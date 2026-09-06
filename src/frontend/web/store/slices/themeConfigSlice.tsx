import { createSlice } from '@reduxjs/toolkit'
import themeConfig from '@/theme.config'

interface Language {
  code: string
  name: string
  isRTL: boolean
}

interface ThemeConfigState {
  isDarkMode: boolean
  sidebar: boolean
  theme: string
  menu: string
  layout: string
  rtlClass: string
  animation: string
  navbar: string
  locale: string
  semidark: boolean
  languageList: Language[]
}

const initialState: ThemeConfigState = {
  isDarkMode: false,
  sidebar: false,
  theme: themeConfig.theme,
  menu: themeConfig.menu,
  layout: themeConfig.layout,
  rtlClass: themeConfig.rtlClass,
  animation: themeConfig.animation,
  navbar: themeConfig.navbar,
  locale: themeConfig.locale,
  semidark: themeConfig.semidark,
  languageList: [
    { code: 'en', name: 'English', isRTL: false },
    { code: 'ar', name: 'Arabic', isRTL: true },
    { code: 'ur', name: 'Urdu', isRTL: true },
    { code: 'zh', name: 'Chinese', isRTL: false },
    { code: 'es', name: 'Spanish', isRTL: false },
    { code: 'fr', name: 'French', isRTL: false },
    { code: 'hi', name: 'Hindi', isRTL: false },
    { code: 'ru', name: 'Russian', isRTL: false },
  ],
}

/**
 * Theme/layout configuration slice managing dark mode, menu style, layout,
 * RTL direction, page animations, navbar style, sidebar visibility, and
 * the supported language list. Browser persistence and DOM updates are
 * handled by the app component so reducers remain deterministic.
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
    toggleMenu(state, { payload }) {
      payload = payload || state.menu // vertical, collapsible-vertical, horizontal
      state.menu = payload
    },
    toggleLayout(state, { payload }) {
      payload = payload || state.layout // full, boxed-layout
      state.layout = payload
    },
    toggleRTL(state, { payload }) {
      payload = payload || state.rtlClass // rtl, ltr
      state.rtlClass = payload
    },
    toggleAnimation(state, { payload }) {
      payload = payload || state.animation // animate__fadeIn, animate__fadeInDown, animate__fadeInUp, animate__fadeInLeft, animate__fadeInRight, animate__slideInDown, animate__slideInLeft, animate__slideInRight, animate__zoomIn
      payload = payload?.trim()
      state.animation = payload
    },
    toggleNavbar(state, { payload }) {
      payload = payload || state.navbar // navbar-sticky, navbar-floating, navbar-static
      state.navbar = payload
    },
    toggleSemidark(state, { payload }) {
      payload = payload === true || payload === 'true' ? true : false
      state.semidark = payload
    },
    toggleSidebar(state) {
      state.sidebar = !state.sidebar
    },
    resetToggleSidebar(state) {
      state.sidebar = false
    },
  },
})

export const { toggleTheme, setDarkMode, toggleMenu, toggleLayout, toggleRTL, toggleAnimation, toggleNavbar, toggleSemidark, toggleSidebar, resetToggleSidebar } = themeConfigSlice.actions
