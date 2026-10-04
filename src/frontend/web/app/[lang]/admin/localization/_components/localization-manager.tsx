'use client'

import { useQueryState, parseAsStringEnum } from 'nuqs'
import type { ReactNode } from 'react'
import { Globe, Languages, Type } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { useAppSelector } from '@/store/hooks'
import { isAllowed } from '@/lib/utils'
import { Tabs } from '@/components/ui'
import { Allow } from '@/allow'
import { LocalizationTextTable } from './localization-text-table'
import { LocalizationLanguageEditor } from './localization-language-editor'

/** The two panels the localization screen offers, kept in the URL so a reload or a shared link lands on the same one. */
const LOCALIZATION_TABS = ['texts', 'languages'] as const
type LocalizationTab = (typeof LOCALIZATION_TABS)[number]
const TAB_ICONS: Record<LocalizationTab, ReactNode> = { texts: <Type />, languages: <Languages /> }

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
    parseAsStringEnum<LocalizationTab>([...LOCALIZATION_TABS])
      .withDefault('texts')
      .withOptions({ history: 'replace', clearOnDefault: true }),
  )

  const activeTenant = authState.activeTenant

  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-start gap-3 rounded-xl border border-border bg-surface p-4 text-sm shadow-xs">
        <span className="grid size-8 shrink-0 place-content-center rounded-lg bg-primary/10 text-primary">
          <Globe className="h-4 w-4" />
        </span>
        <div className="min-w-0 self-center wrap-break-word text-muted-foreground">
          <div className="text-foreground">{activeTenant ? t('page.localization.scope.tenant', { tenant: activeTenant.name }) : t('page.localization.scope.platform')}</div>
          {!canUpdate && <div>{t('page.localization.readOnly')}</div>}
        </div>
      </div>

      <Tabs
        items={LOCALIZATION_TABS.map((candidate) => ({ value: candidate, label: t(`page.localization.tabs.${candidate}`), icon: TAB_ICONS[candidate] }))}
        value={tab}
        onValueChange={setTab}
        panelClassName="min-w-0 rounded-xl border border-border bg-surface p-4 shadow-xs sm:p-6"
      >
        {tab === 'texts' ? <LocalizationTextTable canUpdate={canUpdate} /> : <LocalizationLanguageEditor canUpdate={canUpdate} />}
      </Tabs>
    </div>
  )
}
