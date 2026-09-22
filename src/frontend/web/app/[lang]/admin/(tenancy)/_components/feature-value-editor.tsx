'use client'
import { useMemo, useState } from 'react'
import { useTranslation } from '@/i18n'
import { useLocalizedRouter } from '@/hooks'
import {
  useFeatureValueGetQuery,
  useFeatureValueUpdateMutation,
  FeatureDto,
  FeatureGroupDto,
  FeatureValueProvider,
  FeatureValueProviderName,
  FeatureValueTypeName,
} from '@/store/api/tenancy'
import { Button, ApiErrorMessages, Loader, Badge } from '@/components/ui'
import { Input, Select } from '@/components/ui/form'
import { isAllowed, apiErrorAlert, successToast, cn } from '@/lib/utils'
import { useAppSelector } from '@/store/hooks'
import { Allow } from '@/allow'
import { RotateCcw, Info, Search, Layers, Settings2 } from 'lucide-react'

/**
 * Props for the FeatureValueEditor, naming whose entitlements are being edited.
 */
interface FeatureValueEditorProps {
  providerName: FeatureValueProviderName
  providerKey: string
  /** Where cancelling and saving return to. */
  returnUrl: string
}

/** A toggle rendered as a switch rather than a checkbox, so an on/off entitlement reads at a glance. */
interface FeatureSwitchProps {
  checked: boolean
  disabled?: boolean
  onChange: (checked: boolean) => void
  label: string
}

const FeatureSwitch = ({ checked, disabled, onChange, label }: FeatureSwitchProps) => (
  <button
    type="button"
    role="switch"
    aria-checked={checked}
    aria-label={label}
    disabled={disabled}
    onClick={() => onChange(!checked)}
    className={cn(
      'relative inline-flex h-6 w-11 shrink-0 cursor-pointer items-center rounded-full border-2 border-transparent transition-colors duration-200',
      'focus:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2',
      checked ? 'bg-primary' : 'bg-gray-300 dark:bg-gray-600',
      disabled && 'cursor-not-allowed opacity-50',
    )}
  >
    <span
      className={cn(
        'pointer-events-none inline-block h-5 w-5 rounded-full bg-white shadow ring-0 transition-[margin] duration-200',
        checked ? 'ms-5' : 'ms-0',
      )}
    />
  </button>
)

/**
 * The screen that edits what one tenant or one edition is entitled to. Shared by both, because the
 * only thing that differs is the provider it is addressing.
 *
 * Laid out like the role permission screen — a list of groups on the left, the editor on the right,
 * one save — so the two management surfaces read as one idea. Each row states the value in force and
 * where it came from, so an administrator can tell a value this provider set from one it inherits,
 * and can put an override back by clearing it rather than by guessing what it used to be. Children
 * sit on an indented rail and grey out when the toggle above them is off, which is the whole reason
 * the hierarchy is worth having.
 */
export const FeatureValueEditor = ({ providerName, providerKey, returnUrl }: FeatureValueEditorProps) => {
  const { t } = useTranslation()
  const router = useLocalizedRouter()
  const authState = useAppSelector((state) => state.auth)
  const canManage = isAllowed(authState, [Allow.FeatureValue_Manage])

  const { data, isLoading, error } = useFeatureValueGetQuery(
    { providerName, providerKey },
    { refetchOnMountOrArgChange: true },
  )
  const [updateFeatures, { isLoading: isSaving }] = useFeatureValueUpdateMutation()

  const [activeGroup, setActiveGroup] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  // Only what the administrator actually touched is sent, so a feature nobody edited keeps whatever
  // it inherits rather than being pinned to the value that happened to be showing.
  const [edited, setEdited] = useState<Record<string, string | null>>({})

  const groups = useMemo(() => data?.groups ?? [], [data])
  const currentGroup = groups.find((group) => group.groupName === activeGroup) ?? groups[0]
  const changedCount = Object.keys(edited).length

  const valueOf = (feature: FeatureDto): string | null =>
    feature.name in edited ? edited[feature.name] : (feature.value ?? null)

  const readsAsTrue = (value: string | null) => value?.toLowerCase() === 'true'

  /**
   * Whether a feature is in force once the unsaved edits are taken into account: its own toggle is on
   * and so is every toggle above it. The server answers this too, but only for what is stored, so the
   * editor works it out again to keep the greying-out honest while changes are still pending.
   */
  const isInForce = (feature: FeatureDto, group: FeatureGroupDto): boolean => {
    if (feature.valueType.name === FeatureValueTypeName.Toggle && !readsAsTrue(valueOf(feature))) {
      return false
    }
    return isParentInForce(feature, group)
  }

  const isParentInForce = (feature: FeatureDto, group: FeatureGroupDto): boolean => {
    if (!feature.parentName) return true

    const parent = group.features.find((candidate) => candidate.name === feature.parentName)
    if (!parent) return true

    return readsAsTrue(valueOf(parent)) && isParentInForce(parent, group)
  }

  const enabledCount = (group: FeatureGroupDto) =>
    group.features.filter((feature) => isInForce(feature, group)).length

  const setValue = (name: string, value: string | null) => {
    setEdited((previous) => ({ ...previous, [name]: value }))
  }

  const onSave = async () => {
    const features = Object.entries(edited).map(([name, value]) => ({ name, value }))
    if (features.length === 0) {
      router.push(returnUrl)
      return
    }

    const result = await updateFeatures({ providerName, providerKey, features })
    if (result.error) {
      apiErrorAlert(result.error)
      return
    }

    successToast.fire({ text: t('page.features.saveSuccess') })
    router.push(returnUrl)
  }

  if (isLoading) {
    return (
      <div className="flex justify-center items-center">
        <Loader />
      </div>
    )
  }

  if (error) {
    return (
      <div className="flex justify-center items-center">
        <ApiErrorMessages error={error} />
      </div>
    )
  }

  if (!currentGroup) {
    return <div className="flex justify-center items-center py-10 text-gray-500">{t('page.features.none')}</div>
  }

  const visibleFeatures = currentGroup.features.filter((feature) => {
    const term = search.trim().toLowerCase()
    if (!term) return true
    return (
      feature.displayName.toLowerCase().includes(term) ||
      feature.name.toLowerCase().includes(term) ||
      (feature.description ?? '').toLowerCase().includes(term)
    )
  })

  /** Where a value came from, said in a way an administrator can act on. */
  const sourceBadge = (feature: FeatureDto) => {
    if (feature.providerName === providerName) {
      return <Badge variant="primary" type="solid">{t('page.features.setHere')}</Badge>
    }
    if (feature.providerName === FeatureValueProvider.Edition) {
      return <Badge variant="info" type="outline">{t('page.features.fromEdition')}</Badge>
    }
    return <Badge variant="secondary" type="outline">{t('page.features.fromDefault')}</Badge>
  }

  return (
    <div className="flex flex-col gap-4">
      {/* Changing an entitlement does not end a session that is already running, so the screen says
          so rather than leaving an administrator to wonder why nothing happened. */}
      <div className="flex items-start gap-2 rounded-md border border-info/30 bg-info/10 p-3 text-sm text-info">
        <Info className="mt-0.5 h-4 w-4 shrink-0" />
        <span>{t('page.features.mintTimeNotice')}</span>
      </div>

      <div className="grid min-h-100 grid-cols-1 gap-4 md:grid-cols-4">
        <div className="flex flex-col border-gray-200 pt-2 border-e pe-4 dark:border-gray-700 md:col-span-1">
          <div className="mb-3 flex items-center gap-2 text-xs font-semibold tracking-wider text-gray-800 uppercase dark:text-gray-100">
            <Layers className="h-3.5 w-3.5" />
            {t('page.features.groups')}
          </div>
          <div className="flex flex-col space-y-1">
            {groups.map((group) => {
              const isActive = group.groupName === currentGroup.groupName
              return (
                <button
                  key={group.groupName}
                  type="button"
                  className={cn(
                    'flex cursor-pointer items-center justify-between gap-2 rounded-md px-3 py-2 transition-colors',
                    isActive
                      ? 'bg-primary text-white'
                      : 'text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-800',
                  )}
                  onClick={() => {
                    setActiveGroup(group.groupName)
                    setSearch('')
                  }}
                >
                  <span className="truncate">{group.displayName}</span>
                  <span
                    className={cn(
                      'shrink-0 rounded-full px-2 py-0.5 text-xs',
                      isActive ? 'bg-white/20 text-white' : 'bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-400',
                    )}
                  >
                    {enabledCount(group)}/{group.features.length}
                  </span>
                </button>
              )
            })}
          </div>
        </div>

        <div className="pt-2 md:col-span-3">
          <div className="mb-3 flex items-center justify-between gap-2">
            <div className="flex items-center gap-2 text-xs font-semibold tracking-wider text-gray-800 uppercase dark:text-gray-100">
              <Settings2 className="h-3.5 w-3.5" />
              {currentGroup.displayName}
            </div>
            <div className="relative w-full max-w-xs">
              <input
                type="text"
                name="searchFeatures"
                className="form-input w-full py-1.5! text-sm ps-9"
                placeholder={t('common.search')}
                value={search}
                onChange={(event) => setSearch(event.target.value)}
              />
              <Search className="absolute top-1/2 h-4 w-4 -translate-y-1/2 text-gray-300 start-2 dark:text-gray-600" />
            </div>
          </div>

          <div className="flex flex-col gap-2">
            {visibleFeatures.map((feature) => {
              const value = valueOf(feature)
              const parentOff = !isParentInForce(feature, currentGroup)
              const disabled = !canManage || parentOff
              const isDirty = feature.name in edited

              return (
                <div
                  key={feature.name}
                  style={{ marginInlineStart: `${feature.depth * 1.25}rem` }}
                  className={cn(
                    'flex flex-wrap items-center justify-between gap-3 rounded-md border p-3 transition-colors',
                    'border-white-light bg-white hover:border-primary/40 dark:border-[#1b2e4b] dark:bg-[#191e3a]',
                    feature.depth > 0 && 'border-s-2 border-s-primary/30',
                    parentOff && 'opacity-60',
                  )}
                >
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-semibold text-gray-800 dark:text-gray-100">{feature.displayName}</span>
                      {sourceBadge(feature)}
                      {isDirty && <Badge variant="warning" type="outline">{t('page.features.unsaved')}</Badge>}
                    </div>
                    {feature.description && (
                      <p className="mt-0.5 text-xs text-gray-500 dark:text-gray-400">{feature.description}</p>
                    )}
                    {parentOff && (
                      <p className="mt-0.5 text-xs text-warning">{t('page.features.parentDisabled')}</p>
                    )}
                  </div>

                  <div className="flex shrink-0 items-center gap-2">
                    {feature.valueType.name === FeatureValueTypeName.Toggle && (
                      <FeatureSwitch
                        checked={readsAsTrue(value)}
                        disabled={disabled}
                        label={feature.displayName}
                        onChange={(checked) => setValue(feature.name, checked ? 'true' : 'false')}
                      />
                    )}

                    {feature.valueType.name === FeatureValueTypeName.FreeText && (
                      <div className="w-40">
                        <Input
                          name={feature.name}
                          type={feature.valueType.validatorName === 'Numeric' ? 'number' : 'text'}
                          min={feature.valueType.validatorProperties.minimum}
                          max={feature.valueType.validatorProperties.maximum}
                          maxLength={
                            feature.valueType.validatorProperties.maximumLength
                              ? Number(feature.valueType.validatorProperties.maximumLength)
                              : undefined
                          }
                          value={value ?? ''}
                          disabled={disabled}
                          onChange={(event) => setValue(feature.name, event.target.value)}
                        />
                      </div>
                    )}

                    {feature.valueType.name === FeatureValueTypeName.Selection && (
                      <div className="w-44">
                        <Select
                          name={feature.name}
                          value={value ?? ''}
                          disabled={disabled}
                          onChange={(_, selected) => setValue(feature.name, selected)}
                          options={feature.valueType.items.map((item) => ({ value: item.value, label: item.displayName }))}
                        />
                      </div>
                    )}

                    {canManage && (feature.isOverridden || isDirty) && (
                      <button
                        type="button"
                        className="btn btn-secondary btn-sm cursor-pointer"
                        title={t('page.features.resetToInherited')}
                        onClick={() => setValue(feature.name, null)}
                      >
                        <RotateCcw className="h-3 w-3" />
                      </button>
                    )}
                  </div>
                </div>
              )
            })}

            {visibleFeatures.length === 0 && (
              <div className="py-8 text-center text-sm text-gray-500 dark:text-gray-400">
                {t('page.features.noMatches')}
              </div>
            )}
          </div>
        </div>
      </div>

      <div className="flex items-center justify-end gap-4">
        {changedCount > 0 && (
          <span className="text-sm text-gray-500 dark:text-gray-400">
            {t('page.features.unsavedCount', { count: changedCount })}
          </span>
        )}
        <Button type="button" variant="outline" onClick={() => router.push(returnUrl)} disabled={isSaving}>
          {t('common.cancel')}
        </Button>
        {canManage && (
          <Button type="button" onClick={onSave} isLoading={isSaving} disabled={changedCount === 0}>
            {t('common.save')}
          </Button>
        )}
      </div>
    </div>
  )
}
