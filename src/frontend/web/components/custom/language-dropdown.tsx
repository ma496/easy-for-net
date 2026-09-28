'use client'
import { Dropdown, type DropdownRef } from '@/components/ui'
import { TranslationContext } from '@/components/layouts'
import { ChevronDown } from 'lucide-react'
import { useTranslation, PREFERRED_LANGUAGE_COOKIE, writeLocaleCookie } from '@/i18n'
import { useAppSelector } from '@/store/hooks'
import { useContext, useRef } from 'react'
import { cva } from 'class-variance-authority'
import { cn } from '@/lib/utils'
import Image from 'next/image'

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
 * Client-side dropdown that lists the languages enabled for the acting scope (platform or tenant)
 * and switches to the one picked: it writes a `preferred-language` cookie so the choice survives a
 * later visit with no locale in the URL, then navigates to the same page under that locale segment.
 * RTL and the flags shown come from the same list, so a scope that enables only a subset of the
 * shipped languages never offers one it does not serve.
 */
export const LanguageDropdown = ({ className = '', onlyFlag = false }: LanguageDropdownProps) => {
  const { i18n } = useTranslation()
  const dictionary = useContext(TranslationContext)
  const dropdownRef = useRef<DropdownRef>(null)

  const handleLinkClick = () => {
    if (dropdownRef.current) {
      dropdownRef.current.close()
    }
  }

  const isRtl = useAppSelector((state) => state.theme.rtlClass) === 'rtl'

  const setLocale = (code: string) => {
    writeLocaleCookie(PREFERRED_LANGUAGE_COOKIE, code)
    i18n.changeLanguage(code)
    handleLinkClick()
  }

  return (
    <div className={cn(`dropdown ${onlyFlag ? 'h-9 w-9' : ''}`, className)}>
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
                    <Image src={`/assets/images/flags/${i18n.language.toUpperCase()}.svg`} alt="language flag" width={20} height={20} className="h-5 w-5 rounded-full object-cover" />
                  </div>
                  <div className="text-base font-bold uppercase">{i18n.language}</div>
                  <span className="shrink-0">
                    <ChevronDown size={16} />
                  </span>
                </>
              )}
              {onlyFlag && (
                <div>
                  <Image className="h-5 w-5 rounded-full object-cover" src={`/assets/images/flags/${i18n.language.toUpperCase()}.svg`} alt="language flag" width={20} height={20} />
                </div>
              )}
            </>
          }
        >
          <ul className="grid w-70 grid-cols-2 gap-2 px-2! font-semibold text-dark dark:text-white-light/90">
            {dictionary.languages.map((item) => (
              <li key={item.code}>
                <button
                  type="button"
                  className={cn('flex w-full cursor-pointer rounded-lg hover:text-primary', i18n.language === item.code && 'bg-primary/10 text-primary')}
                  onClick={() => setLocale(item.code)}
                >
                  <Image src={`/assets/images/flags/${item.code.toUpperCase()}.svg`} alt={`${item.name} flag`} width={20} height={20} className="h-5 w-5 rounded-full object-cover" />
                  <span className="ms-3">{item.name}</span>
                </button>
              </li>
            ))}
          </ul>
        </Dropdown>
      )}
    </div>
  )
}
