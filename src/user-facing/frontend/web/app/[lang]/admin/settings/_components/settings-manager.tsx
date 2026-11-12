'use client'

import { useState, type ReactNode } from 'react'
import { useQueryState, parseAsStringEnum } from 'nuqs'
import { Mail, Search, Settings2, SlidersHorizontal } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { useAppSelector } from '@/store/hooks'
import { isAllowed } from '@/lib/utils'
import { ApiErrorMessages, Loader, Tabs, type TabItem } from '@/components/ui'
import { Allow } from '@/allow'
import { findSetting, SettingName, toEmailSettings, toGeneralSettings, toSeoSettings, useSettingListQuery } from '@/store/api/settings'
import { GeneralSettingsCard } from './general-settings-card'
import { EmailSettingsCard } from './email-settings-card'
import { SeoSettingsCard } from './seo-settings-card'

/** The tab of each setting, in the order the strip shows them, kept in the URL so a reload or a shared link lands on the same one. */
const SETTINGS_TABS = ['general', 'email', 'seo'] as const
type SettingsTab = (typeof SETTINGS_TABS)[number]
const TAB_ICONS: Record<SettingsTab, ReactNode> = { general: <SlidersHorizontal />, email: <Mail />, seo: <Search /> }
const TAB_DEFAULT_LABELS: Record<SettingsTab, string> = { general: 'General', email: 'Email', seo: 'SEO' }

/**
 * Client-side shell for `/admin/settings`: a read-only notice for a caller who may not change the
 * settings, and a tab per known setting in a fixed order - General, Email, then SEO. A setting the API does not declare has no tab; one
 * it declares that this screen does not know is ignored, and a `tab` in the URL naming a setting not
 * shown falls back to the first one. Every card is its own form and is remounted, keyed by a counter,
 * after its overrides are reset, so it re-seeds from the freshly fetched values.
 */
export const SettingsManager = () => {
  const { t } = useTranslation()
  const authState = useAppSelector((state) => state.auth)
  const canUpdate = isAllowed(authState, [Allow.Settings_Update])

  const [tab, setTab] = useQueryState(
    'tab',
    parseAsStringEnum<SettingsTab>([...SETTINGS_TABS])
      .withDefault('general')
      .withOptions({ history: 'replace', clearOnDefault: true }),
  )

  const { data, isLoading, error, refetch } = useSettingListQuery()
  const [resetKeys, setResetKeys] = useState<{ [name in SettingName]?: number }>({})

  const remountAfterReset = (name: SettingName) => async () => {
    await refetch()
    setResetKeys((previous) => ({ ...previous, [name]: (previous[name] ?? 0) + 1 }))
  }

  const banner = !canUpdate && (
    <div className="flex items-start gap-3 rounded-xl border border-border bg-surface p-4 text-sm shadow-xs">
      <span className="grid size-8 shrink-0 place-content-center rounded-lg bg-primary/10 text-primary">
        <Settings2 className="h-4 w-4" />
      </span>
      <div className="min-w-0 self-center wrap-break-word text-muted-foreground">{t('page.settings.readOnly') || 'You can view these settings but not change them.'}</div>
    </div>
  )

  const content = () => {
    if (isLoading) {
      return (
        <div className="flex min-h-64 items-center justify-center rounded-xl border border-border bg-surface shadow-xs">
          <Loader />
        </div>
      )
    }

    if (error || !data) {
      return (
        <div className="rounded-xl border border-border bg-surface p-5 shadow-xs">
          <ApiErrorMessages error={error} />
        </div>
      )
    }

    const generalDto = findSetting(data.items, SettingName.General)
    const general = generalDto ? toGeneralSettings(generalDto) : null
    const emailDto = findSetting(data.items, SettingName.Email)
    const email = emailDto ? toEmailSettings(emailDto) : null
    const seoDto = findSetting(data.items, SettingName.Seo)
    const seo = seoDto ? toSeoSettings(seoDto) : null

    if (!general && !email && !seo) {
      return (
        <div className="flex flex-col items-center justify-center gap-3 rounded-xl border border-border bg-surface px-6 py-14 text-center shadow-xs">
          <span className="grid size-12 place-content-center rounded-xl bg-surface-2 text-muted-foreground">
            <Settings2 className="h-5 w-5" />
          </span>
          <p className="text-sm font-medium text-foreground">{t('page.settings.empty') || 'No settings are available'}</p>
        </div>
      )
    }

    const panels: { [name in SettingsTab]?: ReactNode } = {
      // General holds the small settings that need no tab of their own; add further cards here.
      general: general && (
        <div className="flex flex-col gap-6">
          <GeneralSettingsCard key={`${SettingName.General}:${resetKeys.General ?? 0}`} settings={general} canUpdate={canUpdate} onReset={remountAfterReset(SettingName.General)} />
        </div>
      ),
      email: email && (
        <EmailSettingsCard key={`${SettingName.Email}:${resetKeys.Email ?? 0}`} settings={email} canUpdate={canUpdate} onReset={remountAfterReset(SettingName.Email)} />
      ),
      seo: seo && <SeoSettingsCard key={`${SettingName.Seo}:${resetKeys.Seo ?? 0}`} settings={seo} canUpdate={canUpdate} onReset={remountAfterReset(SettingName.Seo)} />,
    }
    const shown = SETTINGS_TABS.filter((candidate) => panels[candidate])
    const activeTab = shown.includes(tab) ? tab : shown[0]
    const items: TabItem<SettingsTab>[] = shown.map((candidate) => ({
      value: candidate,
      label: t(`page.settings.${candidate}.title`) || TAB_DEFAULT_LABELS[candidate],
      icon: TAB_ICONS[candidate],
    }))

    // Only the active card is visible, but every card stays mounted so a half-edited form keeps its edits.
    return (
      <Tabs items={items} value={activeTab} onValueChange={setTab} panelClassName="min-w-0">
        {shown.map((candidate) => (
          <div key={candidate} hidden={candidate !== activeTab}>
            {panels[candidate]}
          </div>
        ))}
      </Tabs>
    )
  }

  return (
    <div className="flex max-w-6xl flex-col gap-6">
      {banner}
      {content()}
    </div>
  )
}
