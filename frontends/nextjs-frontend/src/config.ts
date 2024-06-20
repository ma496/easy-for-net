export type Language = {
  code: string
  name: string
  flag: string
  rightDir?: boolean
}

export const languages: Language[] = [
  { code: 'en', name: 'English', flag: 'en' },
  { code: 'ur', name: 'Urdu', flag: 'pk', rightDir: true },
]

export const locales = languages.map(l => l.code)
