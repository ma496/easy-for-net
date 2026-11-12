---
name: frontend-crud
description: Build the standard list/create/update/delete admin screens for an entity under src/frontend/web/app/[lang]/admin — data table with URL-synced paging, sorting, search, filter panel and export, icon-only toolbar actions and a per-row actions menu gated by permission and row state, Formik + Yup create/update forms, and revalidating the public pages after a save they show. Use when scaffolding CRUD UI for a new entity.
---

# CRUD screens

CRUD screens are administration, so they live in the admin area (`/admin`, behind `AuthGuard` and
`Admin.Access`). The users feature is the reference implementation (roles and SEO pages follow the
same shape); copy its structure:

```
app/[lang]/admin/(<group>)/<entity>/        (or app/[lang]/admin/<entity>/ with no group)
  page.tsx                                  (the list, at /admin/<entity>)
  _components/<entity>-table.tsx
  _components/<entity>-filter-panel.tsx
  create/page.tsx
  create/_components/<entity>-create-form.tsx
  [id]/update/page.tsx
  [id]/update/_components/<entity>-update-form.tsx
```

A screen about one record puts the id before the screen: `/admin/<entity>/{id}/<action>`
(`/admin/users/{id}/update`), so the id names the record and the last segment the screen. Build
every one of these addresses with `adminPath(...)` from `@/lib/paths` — `adminPath('/users/create')`,
``adminPath(`/users/${id}/update`)`` — never a hand-written `/admin/…`. The filter panel is optional
(roles and SEO pages have none); further row screens such as `[id]/detail` are added the same way
and linked from the row actions.

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

if (error) return <div className="py-6"><ApiErrorMessages error={error} /></div>

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
    {filtersOpen && <UserFilterPanel filters={pendingFilters} onChange={setPendingFilters} onSearch={handleSearch} onClear={handleClear} />}
    <DataTable />
    <DataTablePagination siblingCount={1} />
  </DataTableProvider>
)
```

Columns are built with `createDataTableColumnHelper<UserListDto>()` from `@/components/ui/data-table`, wrapped in `columnHelper.columns([...])`; the actions column is
`columnHelper.display({ id: 'actions', header: t('table.actions') || 'Actions', ... })` and renders
`DataTableRowActions` — a three-dot trigger opening a portaled menu of labelled actions, which
renders nothing when every entry is hidden. Gate each entry with `hidden` rather than leaving it out
of the array, give it a translated `label`, and use `href` for navigation (locale-aware) or `onClick`
for a mutation; `variant` is `default | primary | success | warning | danger`:

```tsx
cell: (info) => (
  <DataTableRowActions
    actions={[
      { label: t('common.edit') || 'Edit', icon: <Pencil className="h-4 w-4" />, href: adminPath(`/users/${info.row.original.id}/update`), hidden: !canUpdate },
      { label: t('common.delete') || 'Delete', icon: <Trash2 className="h-4 w-4" />, variant: 'danger', onClick: () => handleDelete(info.row.original.id), disabled: isDeletingUser, hidden: !canDelete },
    ]}
  />
),
```

Headers are translated (`t('table.columns.email') || 'Email'`). Set `enableSorting: false` on computed columns —
and remember any sortable column must also be whitelisted in the backend list validator.

Below `md` the same `<DataTable />` renders each row as a card instead of a table row, with a sort
select standing in for the clickable headers (`cardsBelow="sm" | "lg"` moves the breakpoint, `false`
keeps the table everywhere). The cells keep their renderers and the headers become field labels; the
`actions` column sits top-right and the first data column is the heading. Place the rest with
`meta.card` so a card reads at a glance — `subtitle` for the identifying second line (email,
identifier), `badge` for a status pill at the end of the heading, by the actions menu, `wide` for long text, `hidden` for what a
phone can do without, `title` when the heading is not the first column:

```tsx
columnHelper.accessor('email', { meta: { card: 'subtitle' }, header: t('table.columns.email') || 'Email', cell: (info) => info.getValue() }),
columnHelper.accessor('isActive', { meta: { card: 'badge' }, header: t('table.columns.isActive') || 'Active', cell: ... }),
```

Gating combines the caller's permissions with facts about the row. A row whose DTO extends
`SystemCreatedDto` will be refused on update/delete, so hide those actions up front
(`hidden: !(canDelete && !row.systemCreated)`), and hide any action the row's state rules out (an *activate* only while the row is inactive, a *delete*
only while nothing still references it):

```tsx
const authState = useAppSelector((state) => state.auth)
const canCreate = isAllowed(authState, [Allow.User_Create])
const canUpdate = isAllowed(authState, [Allow.User_Update])
const canDelete = isAllowed(authState, [Allow.User_Delete])
```

Toolbar actions are **icon-only** `DataTableToolbarButton`s — the `label` becomes the tooltip and
the accessible name; pass `href` for navigation or `onClick` for an action, and `primary` on the
screen's main action (create) to fill it with the accent colour. Export is
`DataTableExportButton` (`onExport(format, all)`), filters `DataTableFilterButton`:

```tsx
{canCreate && <DataTableToolbarButton primary label={t('table.createLink') || 'Create'} icon={<Plus size={16} />} href={adminPath('/users/create')} />}
<DataTableExportButton onExport={handleExport} isExporting={isExporting} disabled={isFetching || !data?.total} />
```

When the row's or the server's state rules an action out for now, keep it visible but `disabled` and
let the `label` (its tooltip) say why, rather than hiding it — the visitor then knows the action exists.

Delete uses the shared alert helpers:

```tsx
const result = await confirmDeleteAlert({ title: t('page.users.deleteTitle') || 'Delete User', text: t('page.users.deleteConfirm') || 'Are you sure you want to delete this user?' })
if (!result.isConfirmed) return
const response = await deleteUser({ id })
if (response.error) { apiErrorAlert(response.error); return }
if (response.data?.success) successToast.fire({ text: t('page.users.deleteSuccess') || 'User deleted successfully' })
else if (response.data?.message) await errorAlert({ text: response.data.message })
```

When the entity is something the **public site shows** — an SEO page, or anything a `(site)` page
renders through a cached server fetch — call `revalidatePublicCache(['seo'])` (or `['localization']`;
`lib/revalidate-client.ts`) after a successful create, update or delete, before navigating away. It
posts to `/admin/revalidate`, which checks the caller's own permission for the tag
(`lib/revalidate-rule.ts`), and is best effort: a failure only means the old copy lives out its five
minutes. The SEO table and form (`admin/seo/_components/seo-page-table.tsx`, `seo-page-form.tsx`) do
exactly this. A new cache tag needs an entry in `REVALIDATABLE_TAGS` naming the permission that may
clear it — see the `seo` skill.

Export re-fetches with `all: true` through the lazy query, maps rows to a flat object, and calls
`exportData(format, rows, 'Users', 'users')` (`format` is `'excel' | 'csv'`).

## Filter panel

The shared `DataTableFilterButton` goes in the toolbar (icon-only, shows the active-filter count);
the route supplies an `<Entity>FilterPanel` rendered between the toolbar and the table. The panel is
controlled (`filters`, `onChange`, `onSearch`, `onClear`) and edits **draft state** the table owns —
keep `pendingFilters` in `useState`, sync it from the URL in an effect when the panel opens, and only
write to the URL on *Search*:

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
type its draft state. Its fields are the bare `Select` from `@/components/ui/form` (`size="sm"`),
not the Formik-bound ones. Options that come from the API (a role dropdown, for example) are loaded in
the panel with `useRoleListQuery({ all: true })`, guarded by `<Loader />` and `<ApiErrorMessages />`.

## Create / update forms

Formik + Yup, with the schema built from `t` so messages are localized:

```tsx
const createValidationSchema = (t: (key: string, params?: Record<string, string | number>) => string) =>
  Yup.object().shape({
    username: Yup.string()
      .required(t('validation.required') || 'Field is required')
      .min(3, t('validation.minLength', { min: 3 }) || 'Field must be at least 3 characters'),
    roles: Yup.array().of(Yup.string())
      .required(t('validation.required') || 'Field is required')
      .min(1, t('validation.atLeastOneSelected') || 'At least one item must be selected'),
  })

type FormValues = Yup.InferType<ReturnType<typeof createValidationSchema>>
```

Mirror the backend FluentValidation rules (lengths, required, email) so the user sees the error
before the round trip. Then:

```tsx
<Formik<FormValues> initialValues={{ /* ... */ }} validationSchema={validationSchema} onSubmit={onSubmit}>
  {() => (
    <Form noValidate className="grid grid-cols-1 gap-4 sm:grid-cols-2">
      <FormInput name="username" label={t('form.label.username') || 'Username'} placeholder={t('form.placeholder.username') || 'Enter username'} required autoFocus />
      <FormLazyMultiSelect<RoleListDto, RoleListRequest>
        name="roles"
        label={t('form.label.roles') || 'Roles'}
        useLazyQuery={useLazyRoleListQuery}
        getLabel={(role) => role.name}
        getValue={(role) => role.id}
        pageSize={20}
        required
      />
      <FormCheckbox name="isActive" label={t('form.label.isActive') || 'Active'} />
      <div className="flex justify-end gap-4 sm:col-span-2">
        <Button type="button" variant="outline" onClick={() => router.push(adminPath('/users'))} disabled={isSaving}>
          {t('common.cancel') || 'Cancel'}
        </Button>
        <Button type="submit" isLoading={isSaving}>{t('common.submit') || 'Submit'}</Button>
      </div>
    </Form>
  )}
</Formik>
```

A longer form may group its fields instead: the admin screens wrap them in `FormSection`
(`title`, `description`, `columns={1 | 2}`) and the buttons in `FormActions`, both from the admin
area's own `admin/_components/form-layout.tsx` — promote it to `components/` (see `ui-component`)
once a screen outside the admin area needs it.

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
successToast.fire({ text: t('page.users.createSuccess') || 'User created successfully' })
router.push(adminPath('/users'))   // useLocalizedRouter, unprefixed path
```

The **update** form additionally loads the row and guards the render order:
`isLoading` → `<Loader />`, `error` → `<ApiErrorMessages error={...} />`, no data →
`t('error.server.userNotFound') || 'User not found'` (each centred, in `FormState` where the form uses `form-layout`),
and only then the form, with `initialValues` taken from the
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
- Register list/create/update routes in `auth-urls.ts` (through the `admin(...)` helper, which adds
  `Admin.Access`), `nav-items.ts` and `searchable-items.ts` — see `frontend-page`.
- The web gating only hides what the API would refuse; every endpoint behind the screens checks its
  own permission — see `permissions`.
- `npm run lint`, `npx tsc --noEmit`, `npm run test` and `npm run build` before calling it done.
