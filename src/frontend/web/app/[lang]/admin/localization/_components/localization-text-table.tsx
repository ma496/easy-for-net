'use client'

import { createColumnHelper, ColumnDef } from '@tanstack/react-table'
import { Pencil, RotateCcw } from 'lucide-react'
import { parseAsString, parseAsStringEnum } from 'nuqs'
import { useContext, useState } from 'react'
import { useTranslation } from '@/i18n'
import { TranslationContext } from '@/components/layouts'
import { useLocalizedRouter, useTableUrlState } from '@/hooks'
import { useAppSelector } from '@/store/hooks'
import { apiErrorAlert, confirmDeleteAlert, successToast } from '@/lib/utils'
import { ApiErrorMessages, Badge } from '@/components/ui'
import { Checkbox, Select } from '@/components/ui/form'
import { DataTable, DataTablePagination, DataTableProvider, DataTableRowActions, DataTableToolbar } from '@/components/ui/data-table'
import { TextListItemDto, useLocalizationLanguagesGetQuery, useLocalizationTextDeleteMutation, useLocalizationTextListQuery } from '@/store/api/localization'
import { LocalizationTextEditModal } from './localization-text-edit-modal'

/** Props for the Texts tab, naming whether the caller may edit an override or only see it. */
interface LocalizationTextTableProps {
  canUpdate: boolean
}

/**
 * The Texts tab of the localization admin screen: a culture picker (every shipped language, so an
 * override can be prepared before the language is even enabled), an "only overridden" filter, and a
 * paged table of every shipped key with what the acting scope inherits and what it has overridden.
 * Editing and resetting an override both refresh the app's own layout afterwards, so a text changed
 * here shows up immediately rather than at the next full reload.
 */
export const LocalizationTextTable = ({ canUpdate }: LocalizationTextTableProps) => {
  const { t, i18n } = useTranslation()
  const router = useLocalizedRouter()
  const dictionary = useContext(TranslationContext)
  const isPlatformScope = !useAppSelector((state) => state.auth.activeTenant)

  const url = useTableUrlState({
    filters: {
      culture: parseAsString.withOptions({ clearOnDefault: true, history: 'push' }),
      overridden: parseAsStringEnum(['true'] as const).withOptions({ clearOnDefault: true, history: 'push' }),
    },
  })

  const { data: languagesData, isLoading: isLoadingLanguages } = useLocalizationLanguagesGetQuery()

  const shippedCodes = languagesData?.languages.map((language) => language.code) ?? null
  const fallbackCulture = shippedCodes ? (shippedCodes.includes(i18n.language) ? i18n.language : 'en') : i18n.language
  const activeCulture = url.filters.culture ?? fallbackCulture
  // The root layout already served this culture's own dictionary - including its RTL-ness - before
  // this screen's own "every shipped language" query ever resolves, so that is the first answer,
  // rather than defaulting to `ltr` for the one render where only the server's answer is in yet and
  // then flipping once the client query catches up.
  const activeCultureInfo = dictionary.languages.find((language) => language.code === activeCulture) ?? languagesData?.languages.find((language) => language.code === activeCulture)
  const activeCultureIsRtl = activeCultureInfo?.isRtl ?? false
  const activeCultureName = activeCultureInfo?.name ?? activeCulture

  const cultureOptions = (languagesData?.languages ?? [{ code: activeCulture, name: activeCulture, isRtl: false }]).map((language) => ({
    value: language.code,
    label: `${language.name} (${language.code})`,
  }))

  const onlyOverridden = url.filters.overridden === 'true'

  const { data, isFetching, error } = useLocalizationTextListQuery({
    culture: activeCulture,
    filter: url.search || undefined,
    onlyOverridden: onlyOverridden || undefined,
    page: url.page,
    pageSize: url.pageSize,
  })

  const [deleteOverride, { isLoading: isDeleting }] = useLocalizationTextDeleteMutation()
  const [editingRow, setEditingRow] = useState<TextListItemDto | null>(null)

  const handleReset = async (row: TextListItemDto) => {
    const confirmed = await confirmDeleteAlert({
      title: t('page.localization.texts.resetTitle'),
      text: t('page.localization.texts.resetConfirm'),
    })
    if (!confirmed.isConfirmed) return

    const result = await deleteOverride({ culture: activeCulture, key: row.key })
    if (result.error) {
      apiErrorAlert(result.error)
      return
    }
    successToast.fire({ text: t('page.localization.texts.resetSuccess') })
    router.refresh()
  }

  const columnHelper = createColumnHelper<TextListItemDto>()
  const dir = activeCultureIsRtl ? 'rtl' : 'ltr'

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const columns: ColumnDef<TextListItemDto, any>[] = [
    columnHelper.accessor('key', {
      meta: { card: 'title' },
      header: t('table.columns.key'),
      enableSorting: false,
      minSize: 160,
      maxSize: 260,
      cell: (info) => (
        <span className="font-mono text-xs break-all" dir="ltr">
          {info.getValue()}
        </span>
      ),
    }),
    columnHelper.accessor('inheritedValue', {
      meta: { card: 'wide' },
      header: isPlatformScope ? t('table.columns.defaultValue') : t('table.columns.inheritedValue'),
      enableSorting: false,
      minSize: 200,
      cell: (info) => {
        const row = info.row.original
        return (
          <div dir={dir}>
            <div className="line-clamp-3 whitespace-pre-wrap">{info.getValue()}</div>
            {!isPlatformScope && row.inheritedValue !== row.defaultValue && (
              <Badge variant="info" type="outline" className="mt-1">
                {t('page.localization.texts.fromPlatform')}
              </Badge>
            )}
          </div>
        )
      },
    }),
    columnHelper.accessor('value', {
      meta: { card: 'wide' },
      header: t('table.columns.override'),
      enableSorting: false,
      minSize: 200,
      cell: (info) => {
        const value = info.getValue()
        if (value == null) {
          return (
            <span className="text-subtle-foreground">
              — <span className="sr-only">{t('page.localization.texts.notOverridden')}</span>
            </span>
          )
        }
        return (
          <div dir={dir} className="flex flex-col items-start gap-1">
            <Badge variant="primary" type="solid">
              {t('page.localization.texts.setHere')}
            </Badge>
            <span className="line-clamp-3 font-medium whitespace-pre-wrap text-foreground">{value}</span>
          </div>
        )
      },
    }),
    ...(canUpdate
      ? [
          columnHelper.display({
            id: 'actions',
            header: t('table.actions'),
            cell: (info) => (
              <DataTableRowActions
                actions={[
                  { label: t('common.edit'), icon: <Pencil className="h-4 w-4" />, onClick: () => setEditingRow(info.row.original) },
                  {
                    label: t('page.localization.texts.reset'),
                    icon: <RotateCcw className="h-4 w-4" />,
                    variant: 'warning',
                    onClick: () => handleReset(info.row.original),
                    disabled: isDeleting,
                    hidden: info.row.original.value == null,
                  },
                ]}
              />
            ),
          }),
        ]
      : []),
  ]

  if (error) {
    return (
      <div className="flex justify-center">
        <ApiErrorMessages error={error} />
      </div>
    )
  }

  const emptyMessage = data && data.total === 0 && onlyOverridden && !url.search ? t('page.localization.texts.noOverrides') : undefined

  return (
    <>
      <DataTableProvider
        data={data?.items || []}
        rowCount={data?.total || 0}
        columns={columns}
        enableRowSelection={false}
        sorting={url.sorting}
        setSorting={url.setSorting}
        pagination={url.pagination}
        setPagination={url.setPagination}
        globalFilter={url.searchInput}
        setGlobalFilter={url.setGlobalFilter}
        isFetching={isFetching}
      >
        <DataTableToolbar>
          <div className="w-full sm:w-48">
            <Select
              name="culture"
              size="sm"
              searchable={false}
              clearable={false}
              disabled={isLoadingLanguages}
              value={activeCulture}
              options={cultureOptions}
              onChange={(_, value) => {
                url.filters.setMany({ culture: value })
                url.resetPage()
              }}
            />
          </div>
          <Checkbox
            name="onlyOverridden"
            className="w-full sm:w-auto"
            label={t('page.localization.texts.onlyOverridden')}
            checked={onlyOverridden}
            onChange={(event) => {
              url.filters.setMany({ overridden: event.target.checked ? 'true' : null })
              url.resetPage()
            }}
          />
        </DataTableToolbar>

        <DataTable cardsBelow="lg" emptyMessage={emptyMessage} />

        <DataTablePagination siblingCount={1} />
      </DataTableProvider>

      {editingRow && (
        <LocalizationTextEditModal
          isOpen={true}
          onClose={() => setEditingRow(null)}
          culture={activeCulture}
          cultureName={activeCultureName}
          isRtl={activeCultureIsRtl}
          isPlatformScope={isPlatformScope}
          row={editingRow}
        />
      )}
    </>
  )
}
