---
name: frontend-crud
description: Build the standard list/create/update/delete screens for an entity in src/frontend/web — data table with URL-synced paging, sorting, search, filter panel and export, icon-only toolbar actions and a per-row actions menu gated by permission, row state and plan limits, plus Formik + Yup create/update forms. Use when scaffolding CRUD UI for a new entity.
---

# CRUD screens

The users feature is the reference implementation (roles, tenants and editions follow the same
shape); copy its structure:

```
app/[lang]/admin/(<group>)/<entity>/
  list/page.tsx
  list/_components/<entity>-table.tsx
  list/_components/<entity>-filter-panel.tsx
  create/page.tsx
  create/_components/<entity>-create-form.tsx
  [id]/update/page.tsx
  [id]/update/_components/<entity>-update-form.tsx
```

A screen about one record puts the id before the screen: `/admin/<entity>/{id}/<action>`
(`/admin/users/{id}/update`), so the id names the record and the last segment the screen. The
filter panel is optional (roles and editions have none); further row screens such as
`[id]/detail` or `[id]/members` are added the same way and linked from the row actions.

Page components (server) are covered by the `frontend-page` skill; the API slice by
`rtk-query-api`. This skill covers the client components.

## List table

`'use client'`, driven by `useTableUrlState` + `DataTableProvider` (TanStack Table under the hood).

```tsx
const url = useTableUrlState({
  filters: {
    isActive: parseAsStringEnum(['true', 'false'] as const).withOptions({ clearOnDefault: true, history: 'push' }),
    roleId: parseAsString.withOptions({ clearOnDefault: true, history: 'push' }),
  },
})
```

`useTableUrlState` keeps `page`, `size`, `search`, `sort`, `dir` and your filters in the query
string (nuqs) and returns TanStack-shaped adapters. Use:

| From the hook | For |
| --- | --- |
| `url.page`, `url.pageSize`, `url.sortField`, `url.sortDirection`, `url.search` | the query payload |
| `url.pagination` / `url.setPagination`, `url.sorting` / `url.setSorting` | `DataTableProvider` |
| `url.searchInput` / `url.setGlobalFilter` | the toolbar search box (debounced, 500 ms) |
| `url.filters.<key>`, `url.filters.setMany({...})`, `url.filters.clearFilters()` | the filter panel |
| `url.resetPage()` | after applying or clearing filters |

Fetch, then render (`SortDirection` comes from `@/store/api`, the table pieces from
`@/components/ui/data-table`):

```tsx
const { data, isFetching, error } = useUserListQuery({
  page: url.page,
  pageSize: url.pageSize,
  sortField: url.sortField ?? undefined,
  sortDirection: url.sortDirection === 'desc' ? SortDirection.Desc : SortDirection.Asc,
  search: url.search || undefined,
  isActive: getIsActiveValue(appliedFilters.isActive),
})

if (error) return <div className="flex justify-center items-center"><ApiErrorMessages error={error} /></div>

return (
  <DataTableProvider
    data={data?.items || []}
    rowCount={data?.total || 0}
    columns={columns}
    enableRowSelection={false}
    sorting={url.sorting} setSorting={url.setSorting}
    pagination={url.pagination} setPagination={url.setPagination}
    globalFilter={url.searchInput} setGlobalFilter={url.setGlobalFilter}
    isFetching={isFetching}
  >
    <DataTableToolbar>{/* DataTableFilterButton, create link, export dropdown */}</DataTableToolbar>
    {filtersOpen && <UserFilterPanel />}
    <DataTable />
    <DataTablePagination siblingCount={1} />
  </DataTableProvider>
)
```

Columns are built with `createColumnHelper<UserListDto>()`; the actions column is
`columnHelper.display({ id: 'actions', header: t('table.actions'), ... })` and renders
`DataTableRowActions` — a three-dot trigger opening a portaled menu of labelled actions, which
renders nothing when every entry is hidden. Gate each entry with `hidden` rather than leaving it out
of the array, give it a translated `label`, and use `href` for navigation (locale-aware) or `onClick`
for a mutation; `variant` is `default | primary | success | warning | danger`:

```tsx
cell: (info) => (
  <DataTableRowActions
    actions={[
      { label: t('common.edit'), icon: <Pencil className="h-4 w-4" />, href: `/admin/users/${info.row.original.id}/update`, hidden: !canUpdate },
      { label: t('common.delete'), icon: <Trash2 className="h-4 w-4" />, variant: 'danger', onClick: () => handleDelete(info.row.original.id), disabled: isDeletingUser, hidden: !canDelete },
    ]}
  />
),
```

Headers are translated (`t('table.columns.email')`). Set `enableSorting: false` on computed columns —
and remember any sortable column must also be whitelisted in the backend list validator.

Below `md` the same `<DataTable />` renders each row as a card instead of a table row, with a sort
select standing in for the clickable headers (`cardsBelow="sm" | "lg"` moves the breakpoint, `false`
keeps the table everywhere). The cells keep their renderers and the headers become field labels; the
`actions` column sits top-right and the first data column is the heading. Place the rest with
`meta.card` so a card reads at a glance — `subtitle` for the identifying second line (email,
identifier), `badge` for a status pill at the end of the heading, by the actions menu, `wide` for long text, `hidden` for what a
phone can do without, `title` when the heading is not the first column:

```tsx
columnHelper.accessor('emailNormalized', { meta: { card: 'subtitle' }, header: t('table.columns.email'), cell: (info) => info.getValue() }),
columnHelper.accessor('isActive', { meta: { card: 'badge' }, header: t('table.columns.isActive'), cell: ... }),
```

Gating combines the caller's permissions with facts about the row. A row whose DTO extends
`SystemCreatedDto` will be refused on update/delete, so hide those actions up front
(`hidden: !(canDelete && !row.systemCreated)`), and hide any action the row's state rules out (a
tenant's *suspend* only while it is active, an edition's *delete* only while no tenant uses it):

```tsx
const authState = useAppSelector((state) => state.auth)
const canCreate = isAllowed(authState, [Allow.User_Create])
const canUpdate = isAllowed(authState, [Allow.User_Update])
const canDelete = isAllowed(authState, [Allow.User_Delete])
```

A permission whose capability depends on the tenant's plan needs no extra check — it is absent from
the session when the plan withholds it (see `feature-management`).

Toolbar actions are **icon-only** `DataTableToolbarButton`s — the `label` becomes the tooltip and
the accessible name; pass `href` for navigation or `onClick` for an action. Export is
`DataTableExportButton` (`onExport(format, all)`), filters `DataTableFilterButton`:

```tsx
{canCreate && <DataTableToolbarButton label={t('table.createLink')} icon={<Plus size={16} />} href="/admin/users/create" />}
<DataTableExportButton onExport={handleExport} isExporting={isExporting} disabled={isFetching || !data?.total} />
```

When a plan **limit** stops the action, keep it visible but disabled and let the label explain why,
driven by the same endpoint the API enforces with — the users table reads `useUserSeatsQuery()`:

```tsx
const { data: seats } = useUserSeatsQuery()
const seatsExhausted = seats?.limit != null && seats.used >= seats.limit

{canCreate &&
  (seatsExhausted ? (
    <DataTableToolbarButton label={t('page.users.seatLimitReached')} icon={<Plus size={16} />} disabled />
  ) : (
    <DataTableToolbarButton label={t('table.createLink')} icon={<Plus size={16} />} href="/admin/users/create" />
  ))}
```

Delete uses the shared alert helpers:

```tsx
const result = await confirmDeleteAlert({ title: t('page.users.deleteTitle'), text: t('page.users.deleteConfirm') })
if (!result.isConfirmed) return
const response = await deleteUser({ id })
if (response.error) { apiErrorAlert(response.error); return }
if (response.data?.success) successToast.fire({ text: t('page.users.deleteSuccess') })
else if (response.data?.message) await errorAlert({ text: response.data.message })
```

Export re-fetches with `all: true` through the lazy query, maps rows to a flat object, and calls
`exportData(format, rows, 'Users', 'users')` (`format` is `'excel' | 'csv'`).

## Filter panel

The shared `DataTableFilterButton` goes in the toolbar (icon-only, shows the active-filter count);
the route supplies an `<Entity>FilterPanel` rendered between the toolbar and the table. The panel is **draft state** —
keep `pendingFilters` in `useState`, sync it from the URL when the panel opens, and only write to
the URL on *Search*:

```tsx
const handleSearch = () => {
  url.filters.setMany({
    isActive: pendingFilters.isActive === '' ? null : pendingFilters.isActive,
    roleId: pendingFilters.roleId || null,
  })
  url.resetPage()
}

const handleClear = () => {
  setPendingFilters({ isActive: '', roleId: '' })
  url.filters.clearFilters()
  url.resetPage()
}
```

`null` clears a filter from the URL. Export the panel's `<Entity>Filters` interface so the table can
type its draft state. Options that come from the API (a role dropdown, for example) are loaded in
the panel with `useRoleListQuery({ all: true })`, guarded by `<Loader />` and `<ApiErrorMessages />`.

## Create / update forms

Formik + Yup, with the schema built from `t` so messages are localized:

```tsx
const createValidationSchema = (t: (key: string, params?: Record<string, string | number>) => string) =>
  Yup.object().shape({
    username: Yup.string()
      .required(t('validation.required'))
      .min(3, t('validation.minLength', { min: 3 })),
    roles: Yup.array().of(Yup.string())
      .required(t('validation.required'))
      .min(1, t('validation.atLeastOneSelected')),
  })

type FormValues = Yup.InferType<ReturnType<typeof createValidationSchema>>
```

Mirror the backend FluentValidation rules (lengths, required, email) so the user sees the error
before the round trip. Then:

```tsx
<Formik<FormValues> initialValues={{ /* ... */ }} validationSchema={validationSchema} onSubmit={onSubmit}>
  {() => (
    <Form noValidate className="grid grid-cols-1 gap-4 sm:grid-cols-2">
      <FormInput name="username" label={t('form.label.username')} placeholder={t('form.placeholder.username')} required autoFocus />
      <FormLazyMultiSelect<RoleListDto, RoleListRequest>
        name="roles"
        label={t('form.label.roles')}
        useLazyQuery={useLazyRoleListQuery}
        getLabel={(role) => role.name}
        getValue={(role) => role.id}
        pageSize={20}
        required
      />
      <FormCheckbox name="isActive" label={t('form.label.isActive')} />
      <div className="flex justify-end gap-4 sm:col-span-2">
        <Button type="button" variant="outline" onClick={() => router.push('/admin/users')} disabled={isSaving}>
          {t('common.cancel')}
        </Button>
        <Button type="submit" isLoading={isSaving}>{t('common.submit')}</Button>
      </div>
    </Form>
  )}
</Formik>
```

Formik-bound fields come from `@/components/ui/form`: `FormInput`, `FormPasswordInput`,
`FormTextarea`, `FormSelect`, `FormMultiSelect`, `FormLazySelect`, `FormLazyMultiSelect`,
`FormCheckbox`, `FormRadio`, `FormDatePicker`. Use them inside Formik — the bare `Input`, `Select`,
`Checkbox`, … are for use outside a form (the filter panel). `FileUpload` / `MultiFileUpload` are not
Formik-bound: they upload on selection and hand back the stored file through `onUploaded`, which the
form then writes with `setFieldValue` — see the `file-storage` skill.

Submit handler:

```tsx
const result = await createUser(payload)
if (result.error) { apiErrorAlert(result.error); return }
successToast.fire({ text: t('page.users.createSuccess') })
router.push('/admin/users')          // useLocalizedRouter, unprefixed path
```

The **update** form additionally loads the row and guards the render order:
`isLoading` → `<Loader />`, `error` → `<ApiErrorMessages error={...} />`, no data →
`t('error.server.userNotFound')`, and only then the form, with `initialValues` taken from the
fetched row. A lazy select there takes `selectedItemIds={userData.roles}` so the already-chosen
options are fetched and labelled. The id travels in the path, so the payload is
`{ ...values, id: userId }` and read-only fields shown in the form are stripped before sending.

## Don't forget

- Every label, placeholder, column header, toast and confirm string needs a key in
  every backend resource file (`Features/Localization/Core/Resources/*.json`) — see the
  `localization` skill; error copy is keyed by backend error code,
  see `api-error-handling`.
- A field or widget the shared library does not have yet belongs in `components/ui` — see the
  `ui-component` skill — not in the route's `_components/`.
- Register list/create/update routes in `auth-urls.ts`, `nav-items.ts`, `searchable-items.ts`, and
  in `lib/utils/tenant-routing.ts` when the entity is not tenant-only — see `frontend-page`.
- `npm run lint`, `npx tsc --noEmit`, `npm run test` and `npm run build` before calling it done.
