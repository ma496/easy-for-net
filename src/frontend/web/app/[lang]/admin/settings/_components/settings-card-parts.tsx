'use client'

import type { ReactNode } from 'react'
import { RotateCcw } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { ApiErrorMessages, Badge, Button, Card, CardContent, CardFooter } from '@/components/ui'
import type { ApiError } from '@/lib/utils'
import type { SourceMarker } from './settings-form'

/** Where a value came from, in the same vocabulary the feature editor uses. */
export const SourceBadge = ({ marker }: { marker: SourceMarker }) => {
  const { t } = useTranslation()
  if (marker === 'own') {
    return (
      <Badge variant="primary" type="solid">
        {t('page.settings.source.own')}
      </Badge>
    )
  }
  if (marker === 'platform') {
    return (
      <Badge variant="info" type="outline">
        {t('page.settings.source.platform')}
      </Badge>
    )
  }
  return (
    <Badge variant="secondary" type="outline">
      {t('page.settings.source.default')}
    </Badge>
  )
}

/** Props for one labelled setting: its input id, label, where the value came from and whether it is edited. */
interface SettingFieldProps {
  id: string
  label: string
  marker: SourceMarker
  changed: boolean
  /** Badges beside the source one, such as whether a secret is set. */
  extraBadges?: ReactNode
  children: ReactNode
}

/**
 * One setting property: a label row carrying the source marker (and "Changed" once edited), with the
 * input below it. The input is given the same `id` and no label of its own.
 */
export const SettingField = ({ id, label, marker, changed, extraBadges, children }: SettingFieldProps) => {
  const { t } = useTranslation()
  return (
    <div className="min-w-0">
      <div className="mb-1.5 flex flex-wrap items-center justify-between gap-x-2 gap-y-1">
        <label htmlFor={id} className="mb-0 text-sm font-medium text-foreground">
          {label}
        </label>
        <div className="flex flex-wrap items-center gap-1">
          <SourceBadge marker={marker} />
          {extraBadges}
          {changed && (
            <Badge variant="warning" type="outline">
              {t('page.settings.changed')}
            </Badge>
          )}
        </div>
      </div>
      {children}
    </div>
  )
}

/** Props for a setting card: its copy, state and the actions its footer offers. */
interface SettingsCardProps {
  title: string
  description: string
  /** Whether the scope being edited overrides any property of this setting. */
  isCustomized: boolean
  canUpdate: boolean
  dirty: boolean
  isSaving: boolean
  isResetting: boolean
  saveError?: ApiError
  onReset: () => void
  onDiscard: () => void
  children: ReactNode
}

/**
 * The frame every setting card shares: title, description and the "Customized here" / "Inherited"
 * summary, the save failure at the top of the content, and a footer - only for a caller who may
 * update - with Reset to inherited (when this scope overrides something), Discard (when edited) and Save.
 * Rendered inside the card's own `<Form>`, so Save submits it.
 */
export const SettingsCard = ({ title, description, isCustomized, canUpdate, dirty, isSaving, isResetting, saveError, onReset, onDiscard, children }: SettingsCardProps) => {
  const { t } = useTranslation()
  const isBusy = isSaving || isResetting

  // At lg the setting's title, description and summary sit in a column beside the card holding its fields.
  return (
    <section className="grid gap-4 lg:grid-cols-[minmax(0,17rem)_minmax(0,1fr)] lg:gap-10">
      <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-2">
          <h2 className="text-base font-semibold text-foreground">{title}</h2>
          {isCustomized ? (
            <Badge variant="primary" type="solid">
              {t('page.settings.customized')}
            </Badge>
          ) : (
            <Badge variant="secondary" type="outline">
              {t('page.settings.inherited')}
            </Badge>
          )}
        </div>
        <p className="mt-1 text-sm text-muted-foreground">{description}</p>
      </div>

      <Card className="w-full min-w-0 overflow-hidden">
        <CardContent className="flex flex-col gap-5 pt-5 sm:pt-6">
          {saveError && <ApiErrorMessages error={saveError} />}
          {children}
        </CardContent>

        {canUpdate && (
          <CardFooter className="flex flex-wrap items-center justify-end gap-3 border-t border-border bg-surface-2/50 py-3 sm:py-3">
            {dirty && <span className="me-auto text-sm text-muted-foreground">{t('page.settings.unsaved')}</span>}
            {isCustomized && (
              <Button type="button" variant="outline" onClick={onReset} disabled={isBusy} isLoading={isResetting} icon={<RotateCcw />}>
                {t('page.settings.resetToInherited')}
              </Button>
            )}
            {dirty && (
              <Button type="button" variant="outline" onClick={onDiscard} disabled={isBusy}>
                {t('page.settings.discard')}
              </Button>
            )}
            <Button type="submit" isLoading={isSaving} disabled={!dirty || isBusy}>
              {t('common.save')}
            </Button>
          </CardFooter>
        )}
      </Card>
    </section>
  )
}
