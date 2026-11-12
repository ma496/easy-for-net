'use client'

import { useContext } from 'react'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import { DataTable, DataTablePagination, DataTableProvider, DataTableRowActions, DataTableToolbar, DataTableToolbarButton, createDataTableColumnHelper } from '@/components/ui/data-table'
import { ApiErrorMessages, Badge, DateView } from '@/components/ui'
import { TranslationContext } from '@/components/layouts/translation-provider'
import { useTranslation } from '@/i18n'
import { useTableUrlState } from '@/hooks'
import { useAppSelector } from '@/store/hooks'
import { SortDirection } from '@/store/api'
import { SeoPageListDto, useSeoPageDeleteMutation, useSeoPageListQuery } from '@/store/api/seo'
import { apiErrorAlert, confirmDeleteAlert, isAllowed, successToast } from '@/lib/utils'
import { adminPath } from '@/lib/paths'
import { revalidatePublicCache } from '@/lib/revalidate-client'
import { Allow } from '@/allow'

/**
 * The SEO screen's list: one row per page record - its path, the language it applies to (or all of
 * them), its title, whether it is hidden from search results, whether it is in the sitemap, and when it
 * last changed - with URL-synced paging, sorting and search, and edit/delete actions gated on
 * `Seo.Update`. Deleting asks first and clears the public pages' cached metadata.
 */
export const SeoPageTable = () => {
  const url = useTableUrlState()
  const { t } = useTranslation()
  const dictionary = useContext(TranslationContext)
  const authState = useAppSelector((state) => state.auth)
  const canUpdate = isAllowed(authState, [Allow.Seo_Update])

  const { data, isFetching, error } = useSeoPageListQuery({
    page: url.page,
    pageSize: url.pageSize,
    sortField: url.sortField ?? undefined,
    sortDirection: url.sortDirection === 'desc' ? SortDirection.Desc : SortDirection.Asc,
    search: url.search || undefined,
  })
  const [deletePage, { isLoading: isDeleting }] = useSeoPageDeleteMutation()

  const languageName = (culture: string | null) =>
    culture === null ? t('page.seo.allLanguages') || 'All languages' : (dictionary.languages.find((language) => language.code === culture)?.name ?? culture)

  const handleDelete = async (row: SeoPageListDto) => {
    const confirmed = await confirmDeleteAlert({
      title: t('page.seo.deleteTitle') || 'Delete SEO settings',
      text: t('page.seo.deleteConfirm', { path: row.path }) || `The page ${row.path} will fall back to the site defaults.`,
    })
    if (!confirmed.isConfirmed) return

    const response = await deletePage({ id: row.id })
    if (response.error) {
      apiErrorAlert(response.error)
      return
    }
    await revalidatePublicCache(['seo'])
    successToast.fire({ text: t('page.seo.deleteSuccess') || 'SEO settings deleted' })
  }

  const columnHelper = createDataTableColumnHelper<SeoPageListDto>()
  const columns = columnHelper.columns([
    columnHelper.accessor('path', {
      header: t('table.columns.path') || 'Path',
      cell: (info) => (
        <bdi dir="ltr" className="font-mono text-[13px] break-all text-foreground">
          {info.getValue()}
        </bdi>
      ),
    }),
    columnHelper.accessor('culture', {
      header: t('table.columns.language') || 'Language',
      cell: (info) => (info.getValue() === null ? <Badge variant="secondary">{languageName(null)}</Badge> : <span>{languageName(info.getValue())}</span>),
    }),
    columnHelper.accessor('title', {
      meta: { card: 'subtitle' },
      header: t('table.columns.title') || 'Title',
      cell: (info) => <span className="line-clamp-2 max-w-md wrap-break-word">{info.getValue() || <span className="text-subtle-foreground">{t('page.seo.untitled') || 'Site default'}</span>}</span>,
    }),
    columnHelper.accessor('noIndex', {
      header: t('table.columns.indexing') || 'Indexing',
      enableSorting: false,
      cell: (info) => (info.getValue() ? <Badge variant="warning">{t('page.seo.noIndexBadge') || 'noindex'}</Badge> : <Badge variant="success">{t('page.seo.indexed') || 'Indexed'}</Badge>),
    }),
    columnHelper.accessor('includeInSitemap', {
      header: t('table.columns.inSitemap') || 'In sitemap',
      enableSorting: false,
      cell: (info) => (info.getValue() ? t('common.yes') || 'Yes' : t('common.no') || 'No'),
    }),
    columnHelper.accessor('updatedAt', {
      header: t('table.columns.updated') || 'Updated',
      cell: (info) => <DateView date={info.getValue()} />,
    }),
    columnHelper.display({
      id: 'actions',
      header: t('table.actions') || 'Actions',
      cell: (info) => (
        <DataTableRowActions
          actions={[
            {
              label: t('common.edit') || 'Edit',
              icon: <Pencil className="h-4 w-4" />,
              href: adminPath(`/seo/${info.row.original.id}/update`),
              hidden: !canUpdate,
            },
            {
              label: t('common.delete') || 'Delete',
              icon: <Trash2 className="h-4 w-4" />,
              variant: 'danger',
              onClick: () => handleDelete(info.row.original),
              disabled: isDeleting,
              hidden: !canUpdate,
            },
          ]}
        />
      ),
    }),
  ])

  if (error) {
    return (
      <div className="py-6">
        <ApiErrorMessages error={error} />
      </div>
    )
  }

  return (
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
      <DataTableToolbar>{canUpdate && <DataTableToolbarButton primary label={t('table.createLink') || 'Create'} icon={<Plus size={16} />} href={adminPath('/seo/create')} />}</DataTableToolbar>
      <DataTable emptyMessage={url.searchInput ? undefined : t('page.seo.empty') || 'No page has its own SEO settings yet - every page uses the site defaults.'} />
      <DataTablePagination siblingCount={1} />
    </DataTableProvider>
  )
}
