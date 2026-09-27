'use client'

import { useQueryState, parseAsStringEnum } from 'nuqs'
import { Globe } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { useAppSelector } from '@/store/hooks'
import { cn, isAllowed } from '@/lib/utils'
import { Allow } from '@/allow'
import { LocalizationTextTable } from './localization-text-table'
import { LocalizationLanguageEditor } from './localization-language-editor'

/** The two panels the localization screen offers, kept in the URL so a reload or a shared link lands on the same one. */
const LOCALIZATION_TABS = ['texts', 'languages'] as const
type LocalizationTab = (typeof LOCALIZATION_TABS)[number]

/**
 * Client-side shell for `/admin/localization`: the scope notice (which tenant, or the platform, is
 * being edited, and whether the caller may change it), the Texts/Languages tab strip, and the panel
 * for whichever tab is active - only that one mounts, so switching tabs is a fresh fetch rather than
 * two screens racing each other in the background.
 */
export const LocalizationManager = () => {
  const { t } = useTranslation()
  const authState = useAppSelector((state) => state.auth)
  const canUpdate = isAllowed(authState, [Allow.Localization_Update])

  const [tab, setTab] = useQueryState(
    'tab',
    parseAsStringEnum<LocalizationTab>([...LOCALIZATION_TABS]).withDefault('texts').withOptions({ history: 'replace', clearOnDefault: true }),
  )

  const activeTenant = authState.activeTenant

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-start gap-2 rounded-md border border-info/30 bg-info/10 p-3 text-sm text-info">
        <Globe className="mt-0.5 h-4 w-4 shrink-0" />
        <div className="min-w-0 break-words">
          <div>{activeTenant ? t('page.localization.scope.tenant', { tenant: activeTenant.name }) : t('page.localization.scope.platform')}</div>
          {!canUpdate && <div>{t('page.localization.readOnly')}</div>}
        </div>
      </div>

      <div role="tablist" className="flex border-b border-white-light dark:border-[#191e3a]">
        {LOCALIZATION_TABS.map((candidate) => (
          <button
            key={candidate}
            type="button"
            role="tab"
            aria-selected={tab === candidate}
            className={cn(
              '-mb-px flex-1 px-4 py-2 sm:flex-none',
              tab === candidate ? 'border-b-2 border-primary font-semibold text-primary' : 'text-gray-600 hover:text-primary dark:text-gray-400',
            )}
            onClick={() => setTab(candidate)}
          >
            {t(`page.localization.tabs.${candidate}`)}
          </button>
        ))}
      </div>

      {tab === 'texts' ? <LocalizationTextTable canUpdate={canUpdate} /> : <LocalizationLanguageEditor canUpdate={canUpdate} />}
    </div>
  )
}
