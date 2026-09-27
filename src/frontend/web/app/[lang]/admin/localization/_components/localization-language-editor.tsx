'use client'

import { useEffect, useState } from 'react'
import { RotateCcw } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { useLocalizedRouter } from '@/hooks'
import { useAppSelector } from '@/store/hooks'
import { ApiErrorMessages, Badge, Button, Loader, Tooltip } from '@/components/ui'
import { Checkbox, Select } from '@/components/ui/form'
import { apiErrorAlert, cn, confirmDeleteAlert, successToast } from '@/lib/utils'
import { LanguagesGetResponse, useLocalizationLanguagesGetQuery, useLocalizationLanguagesResetMutation, useLocalizationLanguagesUpdateMutation } from '@/store/api/localization'

/** Props for the Languages tab, naming whether the caller may edit it or only see it. */
interface LocalizationLanguageEditorProps {
  canUpdate: boolean
}

/** Whether two culture lists name the same set, order aside. */
const sameCultureSet = (a: string[], b: string[]): boolean => a.length === b.length && [...a].sort().every((code, index) => code === [...b].sort()[index])

/** What the acting scope's own row would hold, or what it would inherit without one - whichever the draft is seeded from. */
const effectiveOf = (data: LanguagesGetResponse) =>
  data.isInherited
    ? { enabledCultures: data.inheritedEnabledCultures, defaultCulture: data.inheritedDefaultCulture ?? '' }
    : { enabledCultures: data.enabledCultures, defaultCulture: data.defaultCulture ?? '' }

/** A culture's display name, or its code when the catalogue does not (yet) name it. */
const nameOf = (data: LanguagesGetResponse, code: string): string => data.languages.find((language) => language.code === code)?.name ?? code

/**
 * The Languages tab of the localization admin screen: which cultures the acting scope enables, and
 * which one is the default for a visitor whose own preference names none. A scope with no row of its
 * own shows what it inherits, disabled, behind a "Customize" button that seeds an editable draft from
 * those same values - so unlocking the controls never changes what they show. Saving replaces the
 * scope's own row; resetting removes it and falls back to what is inherited, both refreshing the
 * app's own layout so a change to the language actually in use takes effect immediately.
 */
export const LocalizationLanguageEditor = ({ canUpdate }: LocalizationLanguageEditorProps) => {
  const { t, i18n } = useTranslation()
  const router = useLocalizedRouter()
  const isPlatformScope = !useAppSelector((state) => state.auth.activeTenant)
  const { data, isLoading, error } = useLocalizationLanguagesGetQuery()
  const [updateLanguages, { isLoading: isSaving }] = useLocalizationLanguagesUpdateMutation()
  const [resetLanguages, { isLoading: isResetting }] = useLocalizationLanguagesResetMutation()

  const [isCustomizing, setIsCustomizing] = useState(false)
  const [seeded, setSeeded] = useState(false)
  const [draftEnabled, setDraftEnabled] = useState<string[]>([])
  const [draftDefault, setDraftDefault] = useState('')
  const [defaultClearedByToggle, setDefaultClearedByToggle] = useState(false)

  const seedFrom = (source: LanguagesGetResponse) => {
    const effective = effectiveOf(source)
    setDraftEnabled(effective.enabledCultures)
    setDraftDefault(effective.defaultCulture)
    setDefaultClearedByToggle(false)
  }

  useEffect(() => {
    if (!data || seeded) return
    // Seeding only happens once, from whatever the acting scope holds on first load - a later
    // background refetch (after this screen's own save or reset) must not clobber a draft the
    // caller is still editing.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    seedFrom(data)
    setSeeded(true)
  }, [data, seeded])

  if (isLoading) {
    return (
      <div className="flex min-h-64 items-center justify-center">
        <Loader />
      </div>
    )
  }

  if (error || !data) {
    return (
      <div className="rounded-md border border-white-light p-4 dark:border-[#1b2e4b]">
        <ApiErrorMessages error={error} />
      </div>
    )
  }

  const editable = canUpdate && (isCustomizing || !data.isInherited)
  const isBusy = isSaving || isResetting

  const baseline = effectiveOf(data)
  const isDirty = !sameCultureSet(draftEnabled, baseline.enabledCultures) || draftDefault !== baseline.defaultCulture
  const saveDisabled = !editable || !isDirty || draftEnabled.length === 0 || isBusy

  const toggleCulture = (code: string) => {
    if (!editable) return

    if (draftEnabled.includes(code)) {
      if (draftEnabled.length <= 1) return
      setDraftEnabled(draftEnabled.filter((candidate) => candidate !== code))
      if (draftDefault === code) {
        setDraftDefault('')
        setDefaultClearedByToggle(true)
      }
    } else {
      setDraftEnabled([...draftEnabled, code])
    }
  }

  const handleDefaultChange = (value: string) => {
    setDraftDefault(value)
    setDefaultClearedByToggle(false)
  }

  const handleCancel = () => {
    seedFrom(data)
    setIsCustomizing(false)
  }

  const handleSave = async () => {
    const result = await updateLanguages({ enabledCultures: draftEnabled, defaultCulture: draftDefault || null })
    if (result.error) {
      apiErrorAlert(result.error)
      return
    }
    successToast.fire({ text: t('page.localization.languages.saveSuccess') })
    router.refresh()
  }

  const handleReset = async () => {
    const confirmed = await confirmDeleteAlert({
      title: t('page.localization.languages.resetTitle'),
      text: t('page.localization.languages.resetConfirm'),
    })
    if (!confirmed.isConfirmed) return

    const result = await resetLanguages()
    if (result.error) {
      apiErrorAlert(result.error)
      return
    }
    successToast.fire({ text: t('page.localization.languages.resetSuccess') })
    setIsCustomizing(false)
    setDraftEnabled(data.inheritedEnabledCultures)
    setDraftDefault(data.inheritedDefaultCulture ?? '')
    setDefaultClearedByToggle(false)
    router.refresh()
  }

  const inheritedSummary = t('page.localization.languages.inheritedSummary', {
    languages: data.inheritedEnabledCultures.map((code) => nameOf(data, code)).join(', '),
    default: data.inheritedDefaultCulture ? nameOf(data, data.inheritedDefaultCulture) : t('page.localization.languages.noDefault'),
  })

  const currentUrlLanguageWillSwitch = editable && !draftEnabled.includes(i18n.language)

  const defaultLanguageOptions = [
    { value: '', label: t('page.localization.languages.noDefault') },
    ...draftEnabled.map((code) => ({ value: code, label: nameOf(data, code) })),
  ]

  return (
    <div className="flex flex-col gap-5 rounded-md border border-white-light p-4 dark:border-[#1b2e4b]">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          {data.isInherited ? (
            <div className="flex items-center gap-2">
              <Badge variant="secondary" type="outline">
                {t('page.localization.languages.inherited')}
              </Badge>
              <span className="text-sm text-gray-500 dark:text-gray-400">
                {isPlatformScope ? t('page.localization.languages.inheritedFromShipped') : t('page.localization.languages.inheritedFromPlatform')}
              </span>
            </div>
          ) : (
            <Badge variant="primary" type="solid">
              {t('page.localization.languages.customized')}
            </Badge>
          )}
          <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">{inheritedSummary}</p>
        </div>

        {canUpdate &&
          (data.isInherited ? (
            !isCustomizing && (
              <Button type="button" variant="outline" size="sm" onClick={() => setIsCustomizing(true)}>
                {t('page.localization.languages.customize')}
              </Button>
            )
          ) : (
            <Button type="button" variant="outline" onClick={handleReset} disabled={isBusy}>
              <RotateCcw className="h-4 w-4" />
              {t('page.localization.languages.resetToInherited')}
            </Button>
          ))}
      </div>

      <div>
        <div className="mb-2 flex items-center justify-between">
          <span className="text-xs font-semibold tracking-wider text-gray-800 uppercase dark:text-gray-100">{t('page.localization.languages.enabled')}</span>
          <span className="text-xs text-gray-500 dark:text-gray-400">
            {t('page.localization.languages.enabledCount', { count: draftEnabled.length, total: data.languages.length })}
          </span>
        </div>
        <div className="grid grid-cols-1 gap-2 sm:grid-cols-2 lg:grid-cols-4">
          {data.languages.map((language) => {
            const checked = draftEnabled.includes(language.code)
            const isLastEnabled = checked && draftEnabled.length === 1
            const checkbox = (
              <Checkbox
                name={`language-${language.code}`}
                label={language.name}
                checked={checked}
                disabled={!editable || isLastEnabled}
                onChange={() => toggleCulture(language.code)}
              />
            )

            return (
              <div
                key={language.code}
                className={cn('flex items-center gap-3 rounded-md border p-3', checked ? 'border-primary/40' : 'border-white-light dark:border-[#1b2e4b]')}
              >
                {isLastEnabled ? <Tooltip content={t('page.localization.languages.atLeastOne')}>{checkbox}</Tooltip> : checkbox}
                <span className="font-mono text-xs text-gray-500">{language.code}</span>
                {language.isRtl && (
                  <Badge variant="info" type="outline">
                    {t('page.localization.languages.rtl')}
                  </Badge>
                )}
              </div>
            )
          })}
        </div>
      </div>

      <div>
        <div className="w-full sm:w-72">
          <Select
            name="defaultCulture"
            label={t('form.label.defaultLanguage')}
            value={draftDefault}
            options={defaultLanguageOptions}
            placeholder={t('page.localization.languages.noDefault')}
            disabled={!editable}
            clearable={false}
            searchable={false}
            onChange={(_, value) => handleDefaultChange(value)}
          />
        </div>
        {defaultClearedByToggle && <div className="mt-1 text-xs text-warning">{t('page.localization.languages.defaultCleared')}</div>}
      </div>

      {currentUrlLanguageWillSwitch && <div className="text-sm text-warning">{t('page.localization.languages.currentWillSwitch')}</div>}

      {canUpdate && (
        <div className="flex flex-wrap items-center justify-end gap-4">
          {isDirty && <span className="text-sm text-gray-500 dark:text-gray-400">{t('page.localization.languages.unsaved')}</span>}
          <Button type="button" variant="outline" onClick={handleCancel} disabled={isBusy}>
            {t('common.cancel')}
          </Button>
          <Button type="button" onClick={handleSave} isLoading={isSaving} disabled={saveDisabled}>
            {t('common.save')}
          </Button>
        </div>
      )}
    </div>
  )
}
