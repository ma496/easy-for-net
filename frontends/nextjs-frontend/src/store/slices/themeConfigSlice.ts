import { languages } from "@/config"
import { createSlice, PayloadAction } from "@reduxjs/toolkit"

export type ThemeConfigState = {
  locale: string
}

const initialState: ThemeConfigState = {
  locale: 'en',
}

export const themeConfigSlice = createSlice({
  name: 'themeConfig',
  initialState,
  reducers: {
    toggleLanguage(state, { payload }) {
      const locale = payload || state.locale
      const language = languages.find(l => l.code === locale)
      const dirClass =  language?.rightDir ? 'rtl' : 'ltr'
      localStorage.setItem('locale', locale)
      localStorage.setItem('dirClass', dirClass)
      state.locale = locale
      document.querySelector('html')?.setAttribute('lang', state.locale)
      document.querySelector('html')?.setAttribute('dir', dirClass)
    }
  }
})

export const {
  toggleLanguage
} = themeConfigSlice.actions
