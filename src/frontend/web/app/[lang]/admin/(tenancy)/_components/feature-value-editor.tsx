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
import { RotateCcw, Search, Layers, Settings2 } from 'lucide-react'

/**
 * Props for the FeatureValueEditor, naming whose entitlements are being edited.
 */
interface FeatureValueEditorProps {
  providerName: FeatureValueProviderName
  providerKey: string
  /** Where cancelling and saving return to. */
  returnUrl: string
}

/** Props for {@link FeatureSwitch}: its state, whether it can be changed, the change callback and its accessible name. */
interface FeatureSwitchProps {
  checked: boolean
  disabled?: boolean
  onChange: (checked: boolean) => void
  label: string
}

/** A toggle rendered as a switch rather than a checkbox, so an on/off entitlement reads at a glance. */
const FeatureSwitch = ({ checked, disabled, onChange, label }: FeatureSwitchProps) => (
  <button
    type="button"
    role="switch"
    aria-checked={checked}
    aria-label={label}
    disabled={disabled}
    onClick={() => onChange(!checked)}
    className={cn(
      'relative inline-flex h-5 w-9 shrink-0 cursor-pointer items-center rounded-full border border-transparent transition-colors duration-200',
      'focus:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-surface',
      checked ? 'bg-primary' : 'border-input bg-surface-3',
      disabled && 'cursor-not-allowed opacity-50',
    )}
  >
    <span
      className={cn(
        'pointer-events-none inline-block size-4 rounded-full bg-surface shadow-xs transition-[margin] duration-200',
        checked ? 'ms-4.5' : 'ms-0.5',
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
      <div className="panel flex items-center justify-center py-16">
        <Loader />
      </div>
    )
  }

  if (error) {
    return (
      <div className="panel flex items-center justify-center">
        <ApiErrorMessages error={error} />
      </div>
    )
  }

  if (!currentGroup) {
    return (
      <div className="panel flex flex-col items-center justify-center gap-3 py-16 text-center">
        <div className="flex size-12 items-center justify-center rounded-xl bg-surface-2 text-muted-foreground">
          <Layers className="size-5" />
        </div>
        <p className="text-sm text-muted-foreground">{t('page.features.none')}</p>
      </div>
    )
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
    <div className="flex flex-col gap-6">
      <div className="grid grid-cols-1 items-start gap-6 md:grid-cols-[13rem_minmax(0,1fr)] lg:grid-cols-[15rem_minmax(0,1fr)]">
        {/* The group list: a vertical rail from md up, a row of scrollable chips on a phone. */}
        <nav className="min-w-0 md:sticky md:top-20">
          <div className="mb-2 hidden items-center gap-2 px-1 text-[11px] font-semibold tracking-wider text-subtle-foreground uppercase md:flex">
            <Layers className="size-3.5" />
            {t('page.features.groups')}
          </div>
          <div className="-mx-1 flex gap-1 overflow-x-auto px-1 pb-1 md:mx-0 md:flex-col md:overflow-visible md:px-0 md:pb-0">
            {groups.map((group) => {
              const isActive = group.groupName === currentGroup.groupName
              return (
                <button
                  key={group.groupName}
                  type="button"
                  aria-current={isActive ? 'true' : undefined}
                  className={cn(
                    'flex shrink-0 cursor-pointer items-center justify-between gap-3 rounded-md border px-3 py-2 text-sm transition-colors md:w-full',
                    'focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none',
                    isActive
                      ? 'border-primary/20 bg-primary/10 font-medium text-primary'
                      : 'border-transparent text-muted-foreground hover:bg-surface-2 hover:text-foreground',
                  )}
                  onClick={() => {
                    setActiveGroup(group.groupName)
                    setSearch('')
                  }}
                >
                  <span className="truncate">{group.displayName}</span>
                  <span
                    className={cn(
                      'shrink-0 rounded-full px-1.5 py-0.5 text-[11px] font-medium tabular-nums',
                      isActive ? 'bg-primary/15 text-primary' : 'bg-surface-2 text-muted-foreground',
                    )}
                  >
                    {enabledCount(group)}/{group.features.length}
                  </span>
                </button>
              )
            })}
          </div>
        </nav>

        <section className="min-w-0 rounded-xl border border-border bg-surface shadow-xs">
          <header className="flex flex-col gap-3 border-b border-border px-4 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-5">
            <div className="flex min-w-0 items-center gap-3">
              <div className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary">
                <Settings2 className="size-4" />
              </div>
              <div className="min-w-0">
                <h2 className="truncate text-base font-semibold text-foreground">{currentGroup.displayName}</h2>
                <p className="text-xs text-muted-foreground tabular-nums">
                  {enabledCount(currentGroup)}/{currentGroup.features.length}
                </p>
              </div>
            </div>
            <div className="w-full sm:max-w-64">
              <Input
                type="text"
                name="searchFeatures"
                className="[&_.form-input]:h-9 [&_.form-input]:text-sm"
                placeholder={t('common.search')}
                value={search}
                icon={<Search className="size-4" />}
                onChange={(event) => setSearch(event.target.value)}
              />
            </div>
          </header>

          <ul className="divide-y divide-border">
            {visibleFeatures.map((feature) => {
              const value = valueOf(feature)
              const parentOff = !isParentInForce(feature, currentGroup)
              const disabled = !canManage || parentOff
              const isDirty = feature.name in edited

              return (
                <li
                  key={feature.name}
                  className={cn('px-4 py-4 transition-colors hover:bg-surface-2/50 sm:px-5', isDirty && 'bg-warning/5')}
                >
                  {/* Children sit on an indented rail, one step per level of depth. */}
                  <div
                    style={{ marginInlineStart: `${feature.depth * 1.25}rem` }}
                    className={cn(
                      'flex flex-wrap items-center justify-between gap-x-6 gap-y-3',
                      feature.depth > 0 && 'border-s-2 border-primary/25 ps-4',
                      parentOff && 'opacity-60',
                    )}
                  >
                    <div className="min-w-0 flex-1 basis-56">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="text-sm font-medium text-foreground">{feature.displayName}</span>
                        {sourceBadge(feature)}
                        {isDirty && <Badge variant="warning" type="outline">{t('page.features.unsaved')}</Badge>}
                      </div>
                      {feature.description && (
                        <p className="mt-1 text-[13px] text-muted-foreground">{feature.description}</p>
                      )}
                      {parentOff && (
                        <p className="mt-1 text-xs font-medium text-warning">{t('page.features.parentDisabled')}</p>
                      )}
                    </div>

                    <div className="ms-auto flex shrink-0 items-center gap-2">
                      {canManage && (feature.isOverridden || isDirty) && (
                        <button
                          type="button"
                          className="icon-btn size-8"
                          title={t('page.features.resetToInherited')}
                          aria-label={t('page.features.resetToInherited')}
                          onClick={() => setValue(feature.name, null)}
                        >
                          <RotateCcw className="size-3.5" />
                        </button>
                      )}

                      {feature.valueType.name === FeatureValueTypeName.Toggle && (
                        <FeatureSwitch
                          checked={readsAsTrue(value)}
                          disabled={disabled}
                          label={feature.displayName}
                          onChange={(checked) => setValue(feature.name, checked ? 'true' : 'false')}
                        />
                      )}

                      {feature.valueType.name === FeatureValueTypeName.FreeText && (
                        <div className="w-36 sm:w-40">
                          <Input
                            name={feature.name}
                            type={feature.valueType.validatorName === 'Numeric' ? 'number' : 'text'}
                            className="[&_.form-input]:h-9 [&_.form-input]:text-sm [&_.form-input]:tabular-nums"
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
                        <div className="w-40 sm:w-44">
                          <Select
                            name={feature.name}
                            value={value ?? ''}
                            disabled={disabled}
                            size="sm"
                            onChange={(_, selected) => setValue(feature.name, selected)}
                            options={feature.valueType.items.map((item) => ({ value: item.value, label: item.displayName }))}
                          />
                        </div>
                      )}
                    </div>
                  </div>
                </li>
              )
            })}

            {visibleFeatures.length === 0 && (
              <li className="flex flex-col items-center gap-3 px-5 py-12 text-center">
                <div className="flex size-12 items-center justify-center rounded-xl bg-surface-2 text-muted-foreground">
                  <Search className="size-5" />
                </div>
                <p className="text-sm text-muted-foreground">{t('page.features.noMatches')}</p>
              </li>
            )}
          </ul>

          {/* One save for every group, kept in reach at the bottom of the screen however long the list is. */}
          <div className="sticky bottom-0 z-10 flex flex-wrap items-center justify-end gap-x-4 gap-y-2 rounded-b-xl border-t border-border bg-surface/95 px-4 py-3 backdrop-blur sm:px-5">
            {changedCount > 0 && (
              <span className="me-auto flex items-center gap-2 text-sm text-muted-foreground">
                <span className="size-2 rounded-full bg-warning" />
                {t('page.features.unsavedCount', { count: changedCount })}
              </span>
            )}
            <div className="flex items-center gap-2">
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
        </section>
      </div>
    </div>
  )
}
