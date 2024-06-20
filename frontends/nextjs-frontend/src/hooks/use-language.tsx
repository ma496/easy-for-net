import { usePathname, useRouter } from "@/navigation"
import { useAppDispatch } from "@/store/hooks"
import { toggleLanguage } from "@/store/slices/themeConfigSlice"
import { useLocale } from "next-intl"
import { languages } from '@/config'

export function useLanguage() {
  const path = usePathname()
  const dispatch = useAppDispatch()
  const router = useRouter()
  const locale = useLocale()

  const changeLanguage = (lang: string) => {
    router.replace(path, { locale: lang })
    dispatch(toggleLanguage(lang))
  }

  const getCurrentLanguage = () => {
    return languages.find(x => x.code === locale)
  }

  const getLanguage = (code: string) => {
    return languages.find(x => x.code === code)
  }

  return {
    changeLanguage,
    getCurrentLanguage,
    getLanguage
  }
}
