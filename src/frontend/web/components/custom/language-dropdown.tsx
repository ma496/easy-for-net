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
      true: 'icon-btn',
      false: 'chip-btn',
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
    <div className={cn('dropdown', className)}>
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
                    <Image src={`/assets/images/flags/${i18n.language.toUpperCase()}.svg`} alt="language flag" width={20} height={20} className="size-5 rounded-full object-cover ring-1 ring-border" />
                  </div>
                  <div className="text-xs font-semibold uppercase">{i18n.language}</div>
                  <span className="shrink-0">
                    <ChevronDown size={14} className="text-muted-foreground" />
                  </span>
                </>
              )}
              {onlyFlag && (
                <div>
                  <Image className="size-5 rounded-full object-cover ring-1 ring-border" src={`/assets/images/flags/${i18n.language.toUpperCase()}.svg`} alt="language flag" width={20} height={20} />
                </div>
              )}
            </>
          }
        >
          <ul className="grid w-72 grid-cols-2 gap-0.5">
            {dictionary.languages.map((item) => (
              <li key={item.code}>
                <button
                  type="button"
                  className={cn(i18n.language === item.code && 'bg-primary/10! font-medium text-primary!')}
                  onClick={() => setLocale(item.code)}
                >
                  <Image src={`/assets/images/flags/${item.code.toUpperCase()}.svg`} alt={`${item.name} flag`} width={20} height={20} className="h-5 w-5 rounded-full object-cover" />
                  <span className="truncate">{item.name}</span>
                </button>
              </li>
            ))}
          </ul>
        </Dropdown>
      )}
    </div>
  )
}
