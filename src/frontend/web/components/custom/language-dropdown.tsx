'use client'
import { Dropdown, DropdownRef } from '@/components/ui/dropdown'
import { ChevronDown } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { useAppDispatch, useAppSelector } from '@/store/hooks'
import { toggleRTL } from '@/store/slices/themeConfigSlice'
import { useLocalizedRouter } from '@/hooks/use-localized-router'
import { useRef } from 'react'
import { cva } from 'class-variance-authority'
import { cn } from '@/lib/utils'

const languageDropdownVariants = cva('', {
  variants: {
    onlyFlag: {
      true: 'block w-9 h-9 p-2 rounded-full bg-white-light/40 dark:bg-dark/40 hover:text-primary hover:bg-white-light/90 dark:hover:bg-dark/60',
      false: 'flex items-center gap-2.5 rounded-lg border border-white-dark/30 bg-white px-2 py-1.5 text-white-dark hover:border-primary hover:text-primary dark:bg-black',
    },
  },
  defaultVariants: {
    onlyFlag: false,
  },
})

interface LanguageDropdownProps {
  className?: string
  onlyFlag?: boolean
}

/**
 * Client-side dropdown that lists the available languages and allows the user to switch the active locale, updating i18n, RTL state, and refreshing the localized router.
 */
export const LanguageDropdown = ({ className = '', onlyFlag = false }: LanguageDropdownProps) => {
  const dispatch = useAppDispatch()
  const router = useLocalizedRouter()
  const { i18n } = useTranslation()
  const dropdownRef = useRef<DropdownRef>(null)

  const handleLinkClick = () => {
    if (dropdownRef.current) {
      dropdownRef.current.close()
    }
  }

  const isRtl = useAppSelector((state) => state.theme.rtlClass) === 'rtl'

  const themeConfig = useAppSelector((state) => state.theme)
  const setLocale = (flag: string) => {
    const selectedLanguage = themeConfig.languageList.find((lang) => lang.code === flag)
    if (selectedLanguage?.isRTL) {
      dispatch(toggleRTL('rtl'))
    } else {
      dispatch(toggleRTL('ltr'))
    }
    router.refresh()
  }

  return (
    <div className={cn(`dropdown ${onlyFlag ? 'w-9 h-9' : ''}`, className)}>
      {i18n.language && (
        <Dropdown
          ref={dropdownRef}
          placement={`${isRtl ? 'bottom-start' : 'bottom-end'}`}
          btnClassName={languageDropdownVariants({ onlyFlag })}
          button={
            <>
              {!onlyFlag && (
                <>
                  <div>
                    <img src={`/assets/images/flags/${i18n.language.toUpperCase()}.svg`} alt="image" className="h-5 w-5 rounded-full object-cover" />
                  </div>
                  <div className="text-base font-bold uppercase">{i18n.language}</div>
                  <span className="shrink-0">
                    <ChevronDown size={16} />
                  </span>
                </>
              )}
              {onlyFlag && (
                <div>
                  <img className="w-5 h-5 rounded-full object-cover" src={`/assets/images/flags/${i18n.language.toUpperCase()}.svg`} alt="flag" />
                </div>
              )}
            </>
          }
        >
          <ul className="grid w-70 grid-cols-2 gap-2 px-2! font-semibold text-dark dark:text-white-light/90">
            {/* eslint-disable-next-line @typescript-eslint/no-explicit-any */}
            {themeConfig.languageList.map((item: any) => {
              return (
                <li key={item.code}>
                  <button
                    type="button"
                    className={cn('flex w-full cursor-pointer rounded-lg hover:text-primary', i18n.language === item.code && 'bg-primary/10 text-primary')}
                    onClick={() => {
                      i18n.changeLanguage(item.code)
                      setLocale(item.code)
                      handleLinkClick()
                    }}
                  >
                    <img src={`/assets/images/flags/${item.code.toUpperCase()}.svg`} alt="flag" className="h-5 w-5 rounded-full object-cover" />
                    <span className="ltr:ml-3 rtl:mr-3">{item.name}</span>
                  </button>
                </li>
              )
            })}
          </ul>
        </Dropdown>
      )}
    </div>
  )
}

