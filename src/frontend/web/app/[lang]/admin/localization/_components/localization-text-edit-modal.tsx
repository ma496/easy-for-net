'use client'

import { useMemo, useState } from 'react'
import { useTranslation } from '@/i18n'
import { useLocalizedRouter } from '@/hooks'
import { Badge, Button, Modal } from '@/components/ui'
import { Textarea } from '@/components/ui/form'
import { apiErrorAlert, confirmDeleteAlert, successToast } from '@/lib/utils'
import { TextListItemDto, useLocalizationTextDeleteMutation, useLocalizationTextUpsertMutation } from '@/store/api/localization'

/** Every `${name}` token a text carries, in the order it first appears, without duplicates. */
const extractPlaceholders = (value: string): string[] => {
  const matches = value.match(/\$\{[^}]+\}/g) ?? []
  return [...new Set(matches.map((token) => token.slice(2, -1)))]
}

/** Props for the text edit modal, naming the culture and row being edited. */
interface LocalizationTextEditModalProps {
  isOpen: boolean
  onClose: () => void
  culture: string
  cultureName: string
  isRtl: boolean
  isPlatformScope: boolean
  row: TextListItemDto
}

/**
 * Modal that edits (or clears) the acting scope's override of one key in one culture. The read-only
 * block above the textarea shows what the scope inherits without an override of its own - the
 * shipped value in platform scope, the platform's own override in tenant scope - and every `${name}`
 * placeholder that value carries is called out below the textarea, with a warning (never a block) for
 * one the draft has dropped. Saving and resetting both close the modal and refresh the app's own
 * layout, so a text changed here shows up immediately.
 */
export const LocalizationTextEditModal = ({ isOpen, onClose, culture, cultureName, isRtl, isPlatformScope, row }: LocalizationTextEditModalProps) => {
  const { t } = useTranslation()
  const router = useLocalizedRouter()
  const [value, setValue] = useState(row.value ?? row.inheritedValue)
  const [upsert, { isLoading: isSaving }] = useLocalizationTextUpsertMutation()
  const [remove, { isLoading: isResetting }] = useLocalizationTextDeleteMutation()

  const placeholders = useMemo(() => extractPlaceholders(row.inheritedValue), [row.inheritedValue])
  const missingPlaceholders = placeholders.filter((name) => !value.includes(`\${${name}}`))

  const trimmed = value.trim()
  const isUnchanged = trimmed === (row.value ?? row.inheritedValue)
  const canSave = trimmed.length > 0 && !isUnchanged
  const isBusy = isSaving || isResetting
  const dir = isRtl ? 'rtl' : 'ltr'

  const handleSave = async () => {
    const result = await upsert({ culture, key: row.key, value: trimmed })
    if (result.error) {
      apiErrorAlert(result.error)
      return
    }
    successToast.fire({ text: t('page.localization.texts.saveSuccess') })
    onClose()
    router.refresh()
  }

  const handleReset = async () => {
    const confirmed = await confirmDeleteAlert({
      title: t('page.localization.texts.resetTitle'),
      text: t('page.localization.texts.resetConfirm'),
    })
    if (!confirmed.isConfirmed) return

    const result = await remove({ culture, key: row.key })
    if (result.error) {
      apiErrorAlert(result.error)
      return
    }
    successToast.fire({ text: t('page.localization.texts.resetSuccess') })
    onClose()
    router.refresh()
  }

  return (
    <Modal isOpen={isOpen} onClose={onClose} size="lg">
      <Modal.Header>{t('page.localization.texts.editTitle')}</Modal.Header>

      <div className="flex flex-col gap-4">
        <div className="flex flex-wrap items-center gap-2">
          <span className="rounded bg-gray-100 px-2 py-1 font-mono text-xs dark:bg-gray-800" dir="ltr">
            {row.key}
          </span>
          <Badge variant="secondary" type="outline">
            {cultureName}
          </Badge>
        </div>

        <div>
          <div className="label form-label">{isPlatformScope ? t('page.localization.texts.defaultLabel') : t('page.localization.texts.inheritedLabel')}</div>
          <div className="max-h-40 overflow-y-auto rounded-md border border-white-light p-3 text-sm whitespace-pre-wrap dark:border-[#1b2e4b]" dir={dir}>
            {row.inheritedValue}
          </div>
        </div>

        <div>
          <Textarea
            name="overrideValue"
            label={t('page.localization.texts.overrideLabel')}
            rows={6}
            maxLength={4000}
            dir={dir}
            value={value}
            onChange={(event) => setValue(event.target.value)}
          />
          <div className="text-end text-xs text-gray-500">{value.length}/4000</div>
          {trimmed.length === 0 && <div className="mt-1 text-sm text-danger">{t('validation.required')}</div>}
        </div>

        {placeholders.length > 0 && (
          <div className="flex flex-col gap-2">
            <span className="text-sm text-gray-600 dark:text-gray-400">{t('page.localization.texts.placeholders')}</span>
            <div className="flex flex-wrap gap-2">
              {placeholders.map((name) =>
                missingPlaceholders.includes(name) ? (
                  <Badge key={name} variant="warning" type="outline" className="font-mono">
                    {`\${${name}}`}
                  </Badge>
                ) : (
                  <Badge key={name} variant="dark" type="outline" className="font-mono dark:border-white-dark dark:text-white-dark">
                    {`\${${name}}`}
                  </Badge>
                ),
              )}
            </div>
            {missingPlaceholders.length > 0 && <span className="text-sm text-warning">{t('page.localization.texts.placeholderMissing')}</span>}
          </div>
        )}

        <div className="flex flex-wrap items-center justify-between gap-4 border-t border-white-light pt-4 dark:border-[#1b2e4b]">
          <div>
            {row.value != null && (
              <Button type="button" variant="outline-warning" onClick={handleReset} disabled={isBusy}>
                {t('page.localization.texts.reset')}
              </Button>
            )}
          </div>
          <div className="flex justify-end gap-4">
            <Button type="button" variant="outline" onClick={onClose} disabled={isBusy}>
              {t('common.cancel')}
            </Button>
            <Button type="button" onClick={handleSave} isLoading={isSaving} disabled={!canSave || isBusy}>
              {t('common.save')}
            </Button>
          </div>
        </div>
      </div>
    </Modal>
  )
}
