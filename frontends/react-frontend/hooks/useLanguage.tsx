import { IRootState } from "@/store";
import { toggleRTL } from "@/store/themeConfigSlice";
import { useLocale } from "next-intl";
import { usePathname, useRouter } from "next/navigation";
import { useSelector } from "react-redux";
import { useDispatch } from "react-redux";

const useLanguage = () => {
  const dispatch = useDispatch()
  const pathname = usePathname()
  const router = useRouter()
  const themeConfig = useSelector((s: IRootState) => s.themeConfig)
  const locale = useLocale()

  const getCurrentLanguage = () => {
    return themeConfig.languageList.find(x => x.code === locale)
  }

  const getLanguage = (code: string) => {
    return themeConfig.languageList.find(x => x.code === code)
  }

  const changeLanguage = (code: string) => {
    const lang = getLanguage(code)
    if (lang?.rtl) {
      dispatch(toggleRTL('rtl'))
    } else {
      dispatch(toggleRTL('ltr'))
    }

    if (pathname.length > 1) {
      let index = 0
      for (let i = 1; i < pathname.length; i++) {
        if (pathname[i] === '/') {
          index = i
          break
        }
        index = i + 1
      }
      const subStr = pathname.substring(0, index)
      const url = pathname.replace(subStr, `/${code}`)
      router.push(url)
      router.refresh()
    }
  }

  return {
    getCurrentLanguage,
    getLanguage,
    changeLanguage
  }
}

export default useLanguage
