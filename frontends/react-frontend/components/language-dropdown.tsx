'use client';
import Dropdown from '@/components/dropdown';
import IconCaretDown from '@/components/icon/icon-caret-down';
import useChangeLanguage from '@/hooks/useChangeLanguage';
import { IRootState } from '@/store';
import { Language } from '@/store/themeConfigSlice';
import { getLang } from '@/utils/commonUtils';
import { useLocale } from 'next-intl';
import React from 'react';
import { useSelector } from 'react-redux';

interface LanguageDropdownProps {
  className?: string;
}

const LanguageDropdown = ({ className = '' }: LanguageDropdownProps) => {
  const locale = useLocale();
  const isRtl = useSelector((state: IRootState) => state.themeConfig.rtlClass) === 'rtl';
  const themeConfig = useSelector((state: IRootState) => state.themeConfig);
  const changeLanguage = useChangeLanguage();

  return (
    <div className={`dropdown ${className}`}>
      {locale && (
        <Dropdown
          offset={[0, 8]}
          placement={`${isRtl ? 'bottom-start' : 'bottom-end'}`}
          btnClassName="flex items-center gap-2.5 rounded-lg border border-white-dark/30 bg-white px-2 py-1.5 text-white-dark hover:border-primary hover:text-primary dark:bg-black"
          button={
            <>
              <div>
                <img src={`/assets/images/flags/${getLang(themeConfig.languageList, locale)?.flag.toUpperCase()}.svg`} alt="image" className="h-5 w-5 rounded-full object-cover" />
              </div>
              <div className="text-base font-bold uppercase">{locale}</div>
              <span className="shrink-0">
                <IconCaretDown />
              </span>
            </>
          }
        >
          <ul className="grid w-[280px] grid-cols-2 gap-2 !px-2 font-semibold text-dark dark:text-white-dark dark:text-white-light/90">
            {themeConfig.languageList.map((item: Language) => {
              return (
                <li key={item.code}>
                  <button
                    type="button"
                    className={`flex w-full rounded-lg hover:text-primary ${locale === item.code ? 'bg-primary/10 text-primary' : ''}`}
                    onClick={() => {
                      changeLanguage(item.code);
                    }}
                  >
                    <img src={`/assets/images/flags/${item.flag.toUpperCase()}.svg`} alt="flag" className="h-5 w-5 rounded-full object-cover" />
                    <span className="ltr:ml-3 rtl:mr-3">{item.name}</span>
                  </button>
                </li>
              );
            })}
          </ul>
        </Dropdown>
      )}
    </div>
  );
};

export default LanguageDropdown;
