'use client';
import React from 'react';
import { ThemeProvider as NextThemesProvider } from "next-themes"
import { type ThemeProviderProps } from "next-themes/dist/types"

interface IProps extends ThemeProviderProps {
}

const ThemesProvider = ({ children, ...props }: IProps) => {
  return (
    <NextThemesProvider {...props}>
      {children}
    </NextThemesProvider>
  )
}

export default ThemesProvider
